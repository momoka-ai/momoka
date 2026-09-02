using Xunit;
using Momoka.Core.Events;

namespace Momoka.Core.Tests;

/// <summary>
/// 监听自动化（单方法接口模型）：实例实现多个 <see cref="IEventHandler{TEvent}"/> → 整体注册 /
/// 类级选项（优先级）/ 同级按注册序 / 按实例退订 / 零处理器与重复注册 fail-fast。
/// </summary>
public sealed class EventSubscribeTests
{
    [Fact]
    public async Task Register_RegistersAllImplementedHandlerInterfaces()
    {
        var hub = new EventHub();
        var subscriber = new RecordingSubscriber();

        hub.Register(subscriber);

        await hub.Publish(new MessageEvent("hello"));
        await hub.Publish(new NumberEvent(7));

        Assert.Equal(new[] { "hello", "number:7" }, subscriber.Calls);
    }

    [Fact]
    public async Task PriorityOrdering_HighestFirst_LowestLast_AcrossListeners()
    {
        var hub = new EventHub();
        var calls = new List<string>();
        hub.Register(new LowestOrderedListener(calls));
        hub.Register(new NormalOrderedListener(calls));
        hub.Register(new HighestOrderedListener(calls));

        await hub.Publish(new NumberEvent(1));

        Assert.Equal(new[] { "highest", "normal", "lowest" }, calls);
    }

    [Fact]
    public async Task SamePriority_PreservesRegistrationOrder_AcrossListeners()
    {
        var hub = new EventHub();
        var calls = new List<string>();
        hub.Register(new FirstListener(calls));
        hub.Register(new SecondListener(calls));

        await hub.Publish(new MessageEvent("x"));

        Assert.Equal(new[] { "first", "second" }, calls);
    }

    [Fact]
    public async Task Unregister_UnsubscribesAllImplementedInterfaces()
    {
        var hub = new EventHub();
        var subscriber = new RecordingSubscriber();
        hub.Register(subscriber);

        hub.Unregister(subscriber);
        hub.Unregister(subscriber); // 幂等

        await hub.Publish(new MessageEvent("a"));
        await hub.Publish(new NumberEvent(1));

        Assert.Empty(subscriber.Calls);
    }

    [Fact]
    public void Register_NoHandlerInterface_Fails()
    {
        var hub = new EventHub();

        Assert.Throws<InvalidOperationException>(() => hub.Register(new object()));
    }

    [Fact]
    public void Register_DuplicateInstance_Fails_AndReregisterAfterUnregister()
    {
        var hub = new EventHub();
        var subscriber = new RecordingSubscriber();
        hub.Register(subscriber);

        Assert.Throws<InvalidOperationException>(() => hub.Register(subscriber));

        hub.Unregister(subscriber);
        hub.Register(subscriber);
    }

    [Fact]
    public void Register_NullListener_Throws()
    {
        var hub = new EventHub();

        Assert.Throws<ArgumentNullException>(() => hub.Register(null!));
        Assert.Throws<ArgumentNullException>(() => hub.Unregister(null!));
    }

    [Fact]
    public async Task HandlerException_IsIsolated()
    {
        var hub = new EventHub();
        var subscriber = new ThrowingSubscriber();
        hub.Register(subscriber);

        await hub.Publish(new MessageEvent("x")); // 不抛出

        Assert.True(subscriber.Called);
    }

    private sealed record class MessageEvent(string Value) : Event<MessageEvent>;

    private sealed record class NumberEvent(int Value) : Event<NumberEvent>;

    private sealed class RecordingSubscriber : IEventHandler<MessageEvent>, IEventHandler<NumberEvent>
    {
        private readonly object _gate = new();
        private readonly List<string> _calls = new();

        public IReadOnlyList<string> Calls
        {
            get
            {
                lock (_gate)
                {
                    return _calls.ToList();
                }
            }
        }

        public Task OnInvoke(MessageEvent e)
        {
            lock (_gate)
            {
                _calls.Add(e.Value);
            }

            return Task.CompletedTask;
        }

        public Task OnInvoke(NumberEvent e)
        {
            lock (_gate)
            {
                _calls.Add($"number:{e.Value}");
            }

            return Task.CompletedTask;
        }
    }

    [Subscribe(Priority = EventPriority.Highest)]
    private sealed class HighestOrderedListener(List<string> calls) : IEventHandler<NumberEvent>
    {
        public Task OnInvoke(NumberEvent _)
        {
            calls.Add("highest");
            return Task.CompletedTask;
        }
    }

    [Subscribe(Priority = EventPriority.Normal)]
    private sealed class NormalOrderedListener(List<string> calls) : IEventHandler<NumberEvent>
    {
        public Task OnInvoke(NumberEvent _)
        {
            calls.Add("normal");
            return Task.CompletedTask;
        }
    }

    [Subscribe(Priority = EventPriority.Lowest)]
    private sealed class LowestOrderedListener(List<string> calls) : IEventHandler<NumberEvent>
    {
        public Task OnInvoke(NumberEvent _)
        {
            calls.Add("lowest");
            return Task.CompletedTask;
        }
    }

    private sealed class FirstListener(List<string> calls) : IEventHandler<MessageEvent>
    {
        public Task OnInvoke(MessageEvent _)
        {
            calls.Add("first");
            return Task.CompletedTask;
        }
    }

    private sealed class SecondListener(List<string> calls) : IEventHandler<MessageEvent>
    {
        public Task OnInvoke(MessageEvent _)
        {
            calls.Add("second");
            return Task.CompletedTask;
        }
    }

    private sealed class ThrowingSubscriber : IEventHandler<MessageEvent>
    {
        public bool Called { get; private set; }

        public Task OnInvoke(MessageEvent _)
        {
            Called = true;
            throw new InvalidOperationException("subscriber failure");
        }
    }
}
