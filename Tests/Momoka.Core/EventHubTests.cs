using Xunit;
using Momoka.Core.Events;

namespace Momoka.Core.Tests;

/// <summary>事件中心（Bukkit 风格）：处理器接口扫描/类级优先级降序/按实例退订/异常隔离/快照/ICancellable 阻断。</summary>
public sealed class EventHubTests
{
    [Fact]
    public async Task Publish_InvokesListenersInRegistrationOrder()
    {
        var hub = new EventHub();
        var first = new RecordingListener("first");
        var second = new RecordingListener("second");
        hub.Register(first);
        hub.Register(second);

        await hub.Publish(new TestEvent("x"));

        Assert.Equal(new[] { "first:x" }, first.Calls);
        Assert.Equal(new[] { "second:x" }, second.Calls);
    }

    [Fact]
    public async Task Publish_HandlerException_IsIsolatedAndDoesNotBlockOthers()
    {
        var hub = new EventHub();
        var throwing = new ThrowingListener();
        var ok = new RecordingListener("ok");
        hub.Register(throwing);
        hub.Register(ok);

        await hub.Publish(new TestEvent("x")); // 不抛出

        Assert.True(throwing.Called);
        Assert.Equal(new[] { "ok:x" }, ok.Calls);
    }

    [Fact]
    public async Task Publish_PriorityOrdering_HighestFirst_LowestLast()
    {
        var hub = new EventHub();
        var calls = new List<string>();
        hub.Register(new LowOrderedListener(calls));
        hub.Register(new HighOrderedListener(calls));
        hub.Register(new NormalOrderedListener(calls));

        await hub.Publish(new TestEvent("x"));

        Assert.Equal(new[] { "high", "normal", "low" }, calls);
    }

    [Fact]
    public async Task Unregister_StopsDelivering()
    {
        var hub = new EventHub();
        var listener = new RecordingListener("log");
        hub.Register(listener);

        await hub.Publish(new TestEvent("a"));
        hub.Unregister(listener);
        await hub.Publish(new TestEvent("b"));

        Assert.Equal(new[] { "log:a" }, listener.Calls);
    }

    [Fact]
    public async Task Unregister_IsIdempotent()
    {
        var hub = new EventHub();
        var listener = new RecordingListener("");
        hub.Register(listener);

        hub.Unregister(listener);
        hub.Unregister(listener); // 未注册 → no-op

        await hub.Publish(new TestEvent("x"));
        Assert.Empty(listener.Calls);
    }

    [Fact]
    public async Task Register_DuplicateInstance_Fails_AndReregisterAfterUnregister()
    {
        var hub = new EventHub();
        var listener = new RecordingListener("log");
        hub.Register(listener);

        Assert.Throws<InvalidOperationException>(() => hub.Register(listener));

        hub.Unregister(listener);
        hub.Register(listener);
        await hub.Publish(new TestEvent("x"));
        Assert.Equal(new[] { "log:x" }, listener.Calls);
    }

    [Fact]
    public void Register_ZeroHandlerInterface_Fails()
    {
        var hub = new EventHub();

        Assert.Throws<InvalidOperationException>(() => hub.Register(new EmptyListener()));
    }

    [Fact]
    public void Register_NullListener_Throws()
    {
        var hub = new EventHub();

        Assert.Throws<ArgumentNullException>(() => hub.Register(null!));
        Assert.Throws<ArgumentNullException>(() => hub.Unregister(null!));
    }

    [Fact]
    public async Task NoSubscribers_Publish_IsNoOp()
    {
        var hub = new EventHub();

        await hub.Publish(new TestEvent("x")); // 不抛出
    }

    [Fact]
    public async Task Publish_SnapshotsSubscriptions_RemoveDuringDispatchDoesNotAffectCurrentBatch()
    {
        var hub = new EventHub();
        var first = new CountingListener();
        var second = new CountingListener();
        var remover = new SelfRemovingListener(hub, second);
        hub.Register(first);
        hub.Register(remover);
        hub.Register(second);

        await hub.Publish(new TestEvent("one"));
        Assert.Equal(1, first.Count);
        Assert.Equal(1, second.Count); // 快照：本次分发仍送达

        await hub.Publish(new TestEvent("two"));
        Assert.Equal(2, first.Count);
        Assert.Equal(1, second.Count); // 已退订
    }

    [Fact]
    public async Task Publish_NullEvent_Throws()
    {
        var hub = new EventHub();

        await Assert.ThrowsAsync<ArgumentNullException>(() => hub.Publish<TestEvent>(null!));
    }

    [Fact]
    public async Task Publish_CancellableEvent_Veto_IsHeardByAllAndSkipsIgnoreCancelled()
    {
        var hub = new EventHub();
        var veto = new VetoListener<VetoEvent>(cancel: true);
        var skipped = new IgnoreCancelledListener<VetoEvent>();
        var heard = new VetoListener<VetoEvent>(cancel: false);
        hub.Register(veto);
        hub.Register(skipped);
        hub.Register(heard);

        var e = new VetoEvent(1);
        await hub.Publish(e);

        Assert.True(e.IsCancelled);
        Assert.Equal(1, veto.Count);
        Assert.Equal(0, skipped.Count); // ignoreCancelled → 跳过已取消事件
        Assert.Equal(1, heard.Count);   // 其余照常接收（全部否决意见都能被听到）
    }

    [Fact]
    public async Task Publish_CancellableEvent_NoVeto_DispatchesAll()
    {
        var hub = new EventHub();
        var first = new VetoListener<NoVetoEvent>(cancel: false);
        var second = new VetoListener<NoVetoEvent>(cancel: false);
        hub.Register(first);
        hub.Register(second);

        var e = new NoVetoEvent(1);
        await hub.Publish(e);

        Assert.False(e.IsCancelled);
        Assert.Equal(1, first.Count);
        Assert.Equal(1, second.Count);
    }

    private sealed record class NoVetoEvent(int Value) : Event<NoVetoEvent>, ICancellable
    {
        public bool IsCancelled { get; set; }
    }

    private sealed record class TestEvent(string Value) : Event<TestEvent>;

    private sealed record class VetoEvent(int Value) : Event<VetoEvent>, ICancellable
    {
        public bool IsCancelled { get; set; }
    }

    private sealed class RecordingListener(string prefix) : IEventHandler<TestEvent>
    {
        private readonly object _gate = new();

        private List<string> CallsList { get; } = new();

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

        public Task OnInvoke(TestEvent e)
        {
            lock (_gate)
            {
                CallsList.Add($"{prefix}:{e.Value}");
            }

            return Task.CompletedTask;
        }
    }

    private sealed class CountingListener : IEventHandler<TestEvent>
    {
        public int Count;

        public Task OnInvoke(TestEvent _)
        {
            Interlocked.Increment(ref Count);
            return Task.CompletedTask;
        }
    }

    private sealed class ThrowingListener : IEventHandler<TestEvent>
    {
        public bool Called { get; private set; }

        public Task OnInvoke(TestEvent _)
        {
            Called = true;
            throw new InvalidOperationException("handler failure");
        }
    }

    private sealed class EmptyListener 
    {
    }

    [Subscribe(Priority = EventPriority.High)]
    private sealed class HighOrderedListener(List<string> calls)
        : IEventHandler<TestEvent>
    {
        public Task OnInvoke(TestEvent _)
        {
            calls.Add("high");
            return Task.CompletedTask;
        }
    }

    [Subscribe(Priority = EventPriority.Normal)]
    private sealed class NormalOrderedListener(List<string> calls)
        : IEventHandler<TestEvent>
    {
        public Task OnInvoke(TestEvent _)
        {
            calls.Add("normal");
            return Task.CompletedTask;
        }
    }

    [Subscribe(Priority = EventPriority.Low)]
    private sealed class LowOrderedListener(List<string> calls)
        : IEventHandler<TestEvent>
    {
        public Task OnInvoke(TestEvent _)
        {
            calls.Add("low");
            return Task.CompletedTask;
        }
    }

    private sealed class SelfRemovingListener : IEventHandler<TestEvent>
    {
        private readonly EventHub _hub;
        private readonly object _target;

        public SelfRemovingListener(EventHub hub, object target)
        {
            _hub = hub;
            _target = target;
        }

        public Task OnInvoke(TestEvent _)
        {
            _hub.Unregister(_target);
            return Task.CompletedTask;
        }
    }

    private sealed class VetoListener<TEvent>(bool cancel) : IEventHandler<TEvent>
        where TEvent : Event<TEvent>, ICancellable
    {
        public int Count;

        public Task OnInvoke(TEvent e)
        {
            Interlocked.Increment(ref Count);
            if (cancel)
            {
                e.IsCancelled = true;
            }

            return Task.CompletedTask;
        }
    }

    [Subscribe(IgnoreCancelled = true)]
    private sealed class IgnoreCancelledListener<TEvent> : IEventHandler<TEvent>
        where TEvent : Event<TEvent>
    {
        public int Count;

        public Task OnInvoke(TEvent _)
        {
            Interlocked.Increment(ref Count);
            return Task.CompletedTask;
        }
    }
}
