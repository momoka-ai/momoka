using Xunit;
using Momoka.Core.Events;

namespace Momoka.Core.Tests;

/// <summary>事件中心：顺序分发（优先级降序）/并行发布/异常隔离/按实例退订/快照。</summary>
public sealed class EventHubTests
{
    [Fact]
    public async Task Sequential_InvokesListenersInRegistrationOrder()
    {
        var hub = new EventHub();
        var first = new StringListener("first");
        var second = new StringListener("second");
        hub.AddSubscribers(first);
        hub.AddSubscribers(second);

        await hub.InvokeAsync("x");

        Assert.Equal(new[] { "first:x" }, first.Calls);
        Assert.Equal(new[] { "second:x" }, second.Calls);
    }

    [Fact]
    public async Task Sequential_HandlerException_IsIsolatedAndDoesNotBlockOthers()
    {
        var hub = new EventHub();
        var throwing = new ThrowingListener();
        var ok = new StringListener("ok");
        hub.AddSubscribers(throwing);
        hub.AddSubscribers(ok);

        await hub.InvokeAsync("x"); // 不抛出

        Assert.True(throwing.Called);
        Assert.Equal(new[] { "ok:x" }, ok.Calls);
    }

    [Fact]
    public async Task PriorityOrdering_HighestFirst_LowestLast()
    {
        var hub = new EventHub();
        var listener = new OrderedListener();
        hub.AddSubscribers(listener);

        await hub.InvokeAsync(1);

        Assert.Equal(new[] { "highest", "high", "normal", "low", "lowest" }, listener.Calls);
    }

    [Fact]
    public async Task Parallel_RunsAllHandlersConcurrently()
    {
        var hub = new EventHub();
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var listener = new BlockingListener(started, release);
        hub.AddSubscribers(listener);
        var second = new CountingListener();
        hub.AddSubscribers(second);

        var publish = hub.InvokeParallelAsync(1);

        // 两个 handler 都在 release 前开始执行 → 并行分发成立
        await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.False(publish.IsCompleted);

        release.SetResult();
        await publish.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(1, second.Count);
    }

    [Fact]
    public async Task Parallel_HandlerException_DoesNotPropagate()
    {
        var hub = new EventHub();
        hub.AddSubscribers(new ThrowingListener());
        var ok = new CountingListener();
        hub.AddSubscribers(ok);

        await hub.InvokeParallelAsync(1); // 不抛出

        Assert.Equal(1, ok.Count);
    }

    [Fact]
    public async Task RemoveSubscribers_StopsDelivering()
    {
        var hub = new EventHub();
        var listener = new StringListener("log");
        hub.AddSubscribers(listener);

        await hub.InvokeAsync("a");
        hub.RemoveSubscribers(listener);
        await hub.InvokeAsync("b");

        Assert.Equal(new[] { "log:a" }, listener.Calls);
    }

    [Fact]
    public async Task RemoveSubscribers_IsIdempotent()
    {
        var hub = new EventHub();
        var listener = new StringListener("");
        hub.AddSubscribers(listener);

        hub.RemoveSubscribers(listener);
        hub.RemoveSubscribers(listener); // 未注册 → no-op

        await hub.InvokeAsync("x");
        Assert.Empty(listener.Calls);
    }

    [Fact]
    public async Task AddSubscribers_DuplicateInstance_Fails_AndReregisterAfterRemove()
    {
        var hub = new EventHub();
        var listener = new StringListener("log");
        hub.AddSubscribers(listener);

        Assert.Throws<InvalidOperationException>(() => hub.AddSubscribers(listener));

        hub.RemoveSubscribers(listener);
        hub.AddSubscribers(listener);
        await hub.InvokeAsync("x");
        Assert.Equal(new[] { "log:x" }, listener.Calls);
    }

    [Fact]
    public void AddSubscribers_ZeroHandlerType_Fails()
    {
        var hub = new EventHub();

        Assert.Throws<InvalidOperationException>(() => hub.AddSubscribers(new EmptyListener()));
    }

    [Fact]
    public void AddSubscribers_NullListener_Throws()
    {
        var hub = new EventHub();

        Assert.Throws<ArgumentNullException>(() => hub.AddSubscribers(null!));
        Assert.Throws<ArgumentNullException>(() => hub.RemoveSubscribers(null!));
    }

    [Fact]
    public async Task NoSubscribers_Publish_IsNoOp()
    {
        var hub = new EventHub();

        await hub.InvokeAsync(42); // 不抛出
    }

    [Fact]
    public async Task Publish_SnapshotsSubscriptions_RemoveDuringDispatchDoesNotAffectCurrentBatch()
    {
        var hub = new EventHub();
        var first = new CountingListener();
        var second = new CountingListener();
        var remover = new SelfRemovingListener(hub, second);
        hub.AddSubscribers(first);
        hub.AddSubscribers(remover);
        hub.AddSubscribers(second);

        await hub.InvokeAsync(1);
        Assert.Equal(1, first.Count);
        Assert.Equal(1, second.Count); // 快照：本次分发仍送达

        await hub.InvokeAsync(2);
        Assert.Equal(2, first.Count);
        Assert.Equal(1, second.Count); // 已退订
    }

    [Fact]
    public async Task Publish_NullEvent_Throws()
    {
        var hub = new EventHub();

        await Assert.ThrowsAsync<ArgumentNullException>(() => hub.InvokeAsync<string>(null!));
        await Assert.ThrowsAsync<ArgumentNullException>(() => hub.InvokeParallelAsync<string>(null!));
    }

    private sealed class StringListener : Subscribers
    {
        private readonly object _gate = new();

        public StringListener(string prefix)
        {
            Prefix = prefix;
        }

        private string Prefix { get; }

        public List<string> Calls
        {
            get
            {
                lock (_gate)
                {
                    return CallsList.ToList();
                }
            }
        }

        private List<string> CallsList { get; } = new();

        [Subscribe(typeof(string))]
        public Task On(string value)
        {
            lock (_gate)
            {
                CallsList.Add($"{Prefix}:{value}");
            }

            return Task.CompletedTask;
        }
    }

    private sealed class CountingListener : Subscribers
    {
        public int Count;

        [Subscribe(typeof(int))]
        public Task On(int _)
        {
            Interlocked.Increment(ref Count);
            return Task.CompletedTask;
        }
    }

    private sealed class ThrowingListener : Subscribers
    {
        public bool Called { get; private set; }

        [Subscribe(typeof(string))]
        public Task On(string _)
        {
            Called = true;
            throw new InvalidOperationException("handler failure");
        }
    }

    private sealed class EmptyListener : Subscribers
    {
    }

    private sealed class OrderedListener : Subscribers
    {
        public readonly List<string> Calls = new();

        [Subscribe(typeof(int), Priority = EventPriority.Highest)]
        public Task OnHighest(int _) => Record("highest");

        [Subscribe(typeof(int), Priority = EventPriority.High)]
        public Task OnHigh(int _) => Record("high");

        [Subscribe(typeof(int))]
        public Task OnNormal(int _) => Record("normal");

        [Subscribe(typeof(int), Priority = EventPriority.Low)]
        public Task OnLow(int _) => Record("low");

        [Subscribe(typeof(int), Priority = EventPriority.Lowest)]
        public Task OnLowest(int _) => Record("lowest");

        private Task Record(string name)
        {
            Calls.Add(name);
            return Task.CompletedTask;
        }
    }

    private sealed class BlockingListener : Subscribers
    {
        private readonly TaskCompletionSource _started;
        private readonly TaskCompletionSource _release;

        public BlockingListener(TaskCompletionSource started, TaskCompletionSource release)
        {
            _started = started;
            _release = release;
        }

        [Subscribe(typeof(int))]
        public Task On(int _)
        {
            _started.TrySetResult();
            return _release.Task;
        }
    }

    private sealed class SelfRemovingListener : Subscribers
    {
        private readonly EventHub _hub;
        private readonly Subscribers _target;

        public SelfRemovingListener(EventHub hub, Subscribers target)
        {
            _hub = hub;
            _target = target;
        }

        [Subscribe(typeof(int))]
        public Task On(int _)
        {
            _hub.RemoveSubscribers(_target);
            return Task.CompletedTask;
        }
    }
}
