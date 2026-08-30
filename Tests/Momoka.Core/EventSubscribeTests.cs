using Xunit;
using Momoka.Core.Behaviors;
using Momoka.Core.Plugins;

namespace Momoka.Core.Tests;

/// <summary>
/// 监听自动化（[Subscribe] + Subscribers + EventHub.AddSubscribers/RemoveSubscribers）：
/// 实例扫描注册 / 优先级降序 / 按实例退订 / 签名校验与零监听、重复注册 fail-fast / 插件作载体。
/// </summary>
public sealed class EventSubscribeTests
{
    [Fact]
    public async Task AddSubscribers_ScansAndRegistersHandlers()
    {
        var hub = new EventHub();
        var subscriber = new RecordingSubscriber();

        hub.AddSubscribers(subscriber);

        await hub.InvokeAsync("hello");
        await hub.InvokeAsync(7);

        Assert.Equal(new[] { "hello", "high:7", "low:7" }, subscriber.Calls);
    }

    [Fact]
    public async Task PriorityOrdering_HighestFirst_LowestLast()
    {
        var hub = new EventHub();
        var subscriber = new OrderedSubscriber();
        hub.AddSubscribers(subscriber);

        await hub.InvokeAsync(1);

        Assert.Equal(new[] { "highest", "high", "normal", "low", "lowest" }, subscriber.Calls);
    }

    [Fact]
    public async Task SamePriority_PreservesRegistrationOrder()
    {
        var hub = new EventHub();
        var subscriber = new SamePrioritySubscriber();
        hub.AddSubscribers(subscriber);

        await hub.InvokeAsync("x");

        Assert.Equal(new[] { "first", "second" }, subscriber.Calls);
    }

    [Fact]
    public async Task RemoveSubscribers_UnsubscribesAllScannedMethods()
    {
        var hub = new EventHub();
        var subscriber = new RecordingSubscriber();
        hub.AddSubscribers(subscriber);

        hub.RemoveSubscribers(subscriber);
        hub.RemoveSubscribers(subscriber); // 幂等

        await hub.InvokeAsync("a");
        await hub.InvokeAsync(1);

        Assert.Empty(subscriber.Calls);
    }

    [Fact]
    public async Task VoidMethod_And_TaskMethod_AreBothSupported()
    {
        var hub = new EventHub();
        var subscriber = new RecordingSubscriber();
        hub.AddSubscribers(subscriber);

        await hub.InvokeAsync("x");
        await hub.InvokeAsync(5);

        Assert.Contains("x", subscriber.Calls);
        Assert.Contains("high:5", subscriber.Calls);
        Assert.Contains("low:5", subscriber.Calls);
    }

    [Fact]
    public void AddSubscribers_ZeroSubscribeMethods_Fails()
    {
        var hub = new EventHub();

        Assert.Throws<InvalidOperationException>(() => hub.AddSubscribers(new EmptySubscriber()));
    }

    [Fact]
    public void AddSubscribers_InvalidParameterCount_FailsFast()
    {
        var hub = new EventHub();

        Assert.Throws<InvalidOperationException>(() => hub.AddSubscribers(new TwoParametersSubscriber()));
    }

    [Fact]
    public void AddSubscribers_WrongParameterType_FailsFast()
    {
        var hub = new EventHub();

        Assert.Throws<InvalidOperationException>(() => hub.AddSubscribers(new WrongTypeSubscriber()));
    }

    [Fact]
    public void AddSubscribers_InvalidReturnType_FailsFast()
    {
        var hub = new EventHub();

        Assert.Throws<InvalidOperationException>(() => hub.AddSubscribers(new WrongReturnTypeSubscriber()));
    }

    [Fact]
    public void AddSubscribers_DuplicateInstance_Fails()
    {
        var hub = new EventHub();
        var subscriber = new RecordingSubscriber();
        hub.AddSubscribers(subscriber);

        Assert.Throws<InvalidOperationException>(() => hub.AddSubscribers(subscriber));
    }

    [Fact]
    public void AddSubscribers_NullSubscriber_Throws()
    {
        var hub = new EventHub();

        Assert.Throws<ArgumentNullException>(() => hub.AddSubscribers(null!));
    }

    [Fact]
    public void RemoveSubscribers_NullSubscriber_Throws()
    {
        var hub = new EventHub();

        Assert.Throws<ArgumentNullException>(() => hub.RemoveSubscribers(null!));
    }

    [Fact]
    public async Task PluginItself_CanBeSubscriber()
    {
        var hub = new EventHub();
        var plugin = new SubscriberPlugin();
        hub.AddSubscribers(plugin);

        await hub.InvokeAsync("from-plugin");

        Assert.Equal(new[] { "from-plugin" }, plugin.Calls);
    }

    [Fact]
    public async Task HandlerException_IsIsolated()
    {
        var hub = new EventHub();
        var subscriber = new ThrowingSubscriber();
        hub.AddSubscribers(subscriber);

        await hub.InvokeAsync("x"); // 不抛出

        Assert.True(subscriber.Called);
    }

    private sealed class RecordingSubscriber : Subscribers
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

        [Subscribe(typeof(string))]
        public Task OnString(string value)
        {
            lock (_gate)
            {
                _calls.Add(value);
            }

            return Task.CompletedTask;
        }

        [Subscribe(typeof(int), Priority = EventPriority.Low)]
        public void OnIntLow(int value)
        {
            lock (_gate)
            {
                _calls.Add($"low:{value}");
            }
        }

        [Subscribe(typeof(int), Priority = EventPriority.Highest)]
        public Task OnIntHigh(int value)
        {
            lock (_gate)
            {
                _calls.Add($"high:{value}");
            }

            return Task.CompletedTask;
        }
    }

    private sealed class OrderedSubscriber : Subscribers
    {
        public readonly List<string> Calls = new();

        [Subscribe(typeof(int), Priority = EventPriority.Highest)]
        public Task OnHighest(int _)
        {
            Calls.Add("highest");
            return Task.CompletedTask;
        }

        [Subscribe(typeof(int), Priority = EventPriority.High)]
        public Task OnHigh(int _)
        {
            Calls.Add("high");
            return Task.CompletedTask;
        }

        [Subscribe(typeof(int))]
        public Task OnNormal(int _)
        {
            Calls.Add("normal");
            return Task.CompletedTask;
        }

        [Subscribe(typeof(int), Priority = EventPriority.Low)]
        public Task OnLow(int _)
        {
            Calls.Add("low");
            return Task.CompletedTask;
        }

        [Subscribe(typeof(int), Priority = EventPriority.Lowest)]
        public Task OnLowest(int _)
        {
            Calls.Add("lowest");
            return Task.CompletedTask;
        }
    }

    private sealed class SamePrioritySubscriber : Subscribers
    {
        public readonly List<string> Calls = new();

        [Subscribe(typeof(string))]
        public Task OnFirst(string _)
        {
            Calls.Add("first");
            return Task.CompletedTask;
        }

        [Subscribe(typeof(string))]
        public Task OnSecond(string _)
        {
            Calls.Add("second");
            return Task.CompletedTask;
        }
    }

    private sealed class EmptySubscriber : Subscribers
    {
    }

    private sealed class TwoParametersSubscriber : Subscribers
    {
        [Subscribe(typeof(string))]
        public Task On(string a, string b) => Task.CompletedTask;
    }

    private sealed class WrongTypeSubscriber : Subscribers
    {
        [Subscribe(typeof(string))]
        public Task On(int value) => Task.CompletedTask;
    }

    private sealed class WrongReturnTypeSubscriber : Subscribers
    {
        [Subscribe(typeof(string))]
        public int On(string value) => 1;
    }

    private sealed class SubscriberPlugin : Plugin, Subscribers
    {
        public readonly List<string> Calls = new();

        [Subscribe(typeof(string))]
        public Task On(string value)
        {
            Calls.Add(value);
            return Task.CompletedTask;
        }
    }

    private sealed class ThrowingSubscriber : Subscribers
    {
        public bool Called { get; private set; }

        [Subscribe(typeof(string))]
        public Task On(string _)
        {
            Called = true;
            throw new InvalidOperationException("subscriber failure");
        }
    }
}
