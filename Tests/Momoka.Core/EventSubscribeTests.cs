using Xunit;
using Momoka.Core.Events;
using Momoka.Core.Plugins;

namespace Momoka.Core.Tests;

/// <summary>
/// 监听自动化（[Subscribe] + EventHub.AddSubscribers）：实例扫描注册 / 优先级排序（Monitor 恒最后）/
/// 整体退订令牌 / 签名校验 fail-fast / 与手动 Subscribe 共存 / 插件自身作 subscriber。
/// </summary>
public sealed class EventSubscribeTests
{
    [Fact]
    public async Task AddSubscribers_ScansAndRegistersHandlers()
    {
        var hub = new EventHub();
        var subscriber = new RecordingSubscriber();

        using var token = hub.AddSubscribers(subscriber);

        await hub.InvokeAsync("hello");
        await hub.InvokeAsync(7);

        Assert.Equal(new[] { "hello", "high:7", "low:7", "monitor:7" }, subscriber.Calls);
    }

    [Fact]
    public async Task PriorityOrdering_HighestFirst_MonitorLast()
    {
        var hub = new EventHub();
        var subscriber = new OrderedSubscriber();

        using var token = hub.AddSubscribers(subscriber);

        await hub.InvokeAsync(1);

        Assert.Equal(new[] { "highest", "high", "normal", "low", "lowest", "monitor" }, subscriber.Calls);
    }

    [Fact]
    public async Task SamePriority_PreservesRegistrationOrder()
    {
        var hub = new EventHub();
        var subscriber = new SamePrioritySubscriber();

        using var token = hub.AddSubscribers(subscriber);

        await hub.InvokeAsync("x");

        Assert.Equal(new[] { "first", "second" }, subscriber.Calls);
    }

    [Fact]
    public async Task BatchToken_UnsubscribesAllScannedMethods()
    {
        var hub = new EventHub();
        var subscriber = new RecordingSubscriber();

        var token = hub.AddSubscribers(subscriber);
        token.Dispose();
        token.Dispose(); // 幂等

        await hub.InvokeAsync("a");
        await hub.InvokeAsync(1);

        Assert.Empty(subscriber.Calls);
    }

    [Fact]
    public async Task VoidMethod_And_TaskMethod_AreBothSupported()
    {
        var hub = new EventHub();
        var subscriber = new RecordingSubscriber();

        using var token = hub.AddSubscribers(subscriber);
        await hub.InvokeAsync("x");
        await hub.InvokeAsync(5);

        Assert.Contains("x", subscriber.Calls);
        Assert.Contains("high:5", subscriber.Calls);
        Assert.Contains("low:5", subscriber.Calls);
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
    public void AddSubscribers_NullSubscriber_Throws()
    {
        var hub = new EventHub();

        Assert.Throws<ArgumentNullException>(() => hub.AddSubscribers(null!));
    }

    [Fact]
    public async Task CoexistsWithManualLambdaSubscribe()
    {
        var hub = new EventHub();
        var subscriber = new RecordingSubscriber();
        var manualCalls = 0;

        using var manual = hub.Subscribe<int>(_ =>
        {
            Interlocked.Increment(ref manualCalls);
            return Task.CompletedTask;
        });
        using var batch = hub.AddSubscribers(subscriber);

        await hub.InvokeAsync(1);

        Assert.Equal(1, manualCalls);
        Assert.Contains("high:1", subscriber.Calls);
    }

    [Fact]
    public async Task PluginItself_CanBeSubscriber()
    {
        var hub = new EventHub();
        var plugin = new SubscriberPlugin();

        using var token = hub.AddSubscribers(plugin);
        await hub.InvokeAsync("from-plugin");

        Assert.Equal(new[] { "from-plugin" }, plugin.Calls);
    }

    [Fact]
    public async Task HandlerException_IsIsolated()
    {
        var hub = new EventHub();
        var subscriber = new ThrowingSubscriber();

        using var token = hub.AddSubscribers(subscriber);
        await hub.InvokeAsync("x"); // 不抛出

        Assert.True(subscriber.Called);
    }

    private sealed class RecordingSubscriber
    {
        private readonly List<string> _calls = new();

        public IReadOnlyList<string> Calls
        {
            get
            {
                lock (_calls)
                {
                    return _calls.ToList();
                }
            }
        }

        [Subscribe(typeof(string))]
        public Task OnString(string value)
        {
            lock (_calls)
            {
                _calls.Add(value);
            }

            return Task.CompletedTask;
        }

        [Subscribe(typeof(int), Priority = EventPriority.Low)]
        public void OnIntLow(int value)
        {
            lock (_calls)
            {
                _calls.Add($"low:{value}");
            }
        }

        [Subscribe(typeof(int), Priority = EventPriority.Highest)]
        public Task OnIntHigh(int value)
        {
            lock (_calls)
            {
                _calls.Add($"high:{value}");
            }

            return Task.CompletedTask;
        }

        [Subscribe(typeof(int), Priority = EventPriority.Monitor)]
        public Task OnIntMonitor(int value)
        {
            lock (_calls)
            {
                _calls.Add($"monitor:{value}");
            }

            return Task.CompletedTask;
        }
    }

    private sealed class OrderedSubscriber
    {
        public readonly List<string> Calls = new();

        [Subscribe(typeof(int), Priority = EventPriority.Monitor)]
        public Task OnMonitor(int value)
        {
            Calls.Add("monitor");
            return Task.CompletedTask;
        }

        [Subscribe(typeof(int), Priority = EventPriority.Highest)]
        public Task OnHighest(int value)
        {
            Calls.Add("highest");
            return Task.CompletedTask;
        }

        [Subscribe(typeof(int), Priority = EventPriority.Normal)]
        public Task OnNormal(int value)
        {
            Calls.Add("normal");
            return Task.CompletedTask;
        }

        [Subscribe(typeof(int), Priority = EventPriority.Low)]
        public Task OnLow(int value)
        {
            Calls.Add("low");
            return Task.CompletedTask;
        }

        [Subscribe(typeof(int), Priority = EventPriority.Lowest)]
        public Task OnLowest(int value)
        {
            Calls.Add("lowest");
            return Task.CompletedTask;
        }

        [Subscribe(typeof(int), Priority = EventPriority.High)]
        public Task OnHigh(int value)
        {
            Calls.Add("high");
            return Task.CompletedTask;
        }
    }

    private sealed class SamePrioritySubscriber
    {
        public readonly List<string> Calls = new();

        [Subscribe(typeof(string))]
        public Task OnFirst(string value)
        {
            Calls.Add("first");
            return Task.CompletedTask;
        }

        [Subscribe(typeof(string))]
        public Task OnSecond(string value)
        {
            Calls.Add("second");
            return Task.CompletedTask;
        }
    }

    private sealed class TwoParametersSubscriber
    {
        [Subscribe(typeof(string))]
        public Task On(string a, string b) => Task.CompletedTask;
    }

    private sealed class WrongTypeSubscriber
    {
        [Subscribe(typeof(string))]
        public Task On(int value) => Task.CompletedTask;
    }

    private sealed class WrongReturnTypeSubscriber
    {
        [Subscribe(typeof(string))]
        public int On(string value) => 1;
    }

    private sealed class SubscriberPlugin : Plugin
    {
        public readonly List<string> Calls = new();

        [Subscribe(typeof(string))]
        public Task On(string value)
        {
            Calls.Add(value);
            return Task.CompletedTask;
        }
    }

    private sealed class ThrowingSubscriber
    {
        public bool Called { get; private set; }

        [Subscribe(typeof(string))]
        public Task On(string value)
        {
            Called = true;
            throw new InvalidOperationException("subscriber failure");
        }
    }
}
