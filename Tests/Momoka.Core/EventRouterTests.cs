using System.Text.Json.Nodes;
using Xunit;
using Momoka.Core.Events;

namespace Momoka.Core.Tests;

/// <summary>
/// 事件路由（[Publish] + EventHub 属性感知分发）：路由矩阵逐项 /
/// wire-in（Listeners+Id）无 echo / 组合校验与重复 Id fail-fast / wire-sender 异常不阻断。
/// </summary>
public sealed class EventRouterTests
{
    [Fact]
    public async Task None_Destination_OnlyRecords()
    {
        var (hub, wire, logger) = CreateHub();
        hub.RegisterEventType(typeof(SinkEvent));
        hub.AddSubscribers(new SinkListener());

        await hub.InvokeAsync(new SinkEvent());

        Assert.Empty(wire);
        Assert.Contains(logger.Messages, m => m.Contains("SinkEvent"));
    }

    [Fact]
    public async Task Listeners_Destination_LocalOnly()
    {
        var (hub, wire, _) = CreateHub();
        hub.RegisterEventType(typeof(LocalEvent));
        var listener = new LocalListener();
        hub.AddSubscribers(listener);

        await hub.InvokeAsync(new LocalEvent());

        Assert.Equal(1, listener.Count);
        Assert.Empty(wire);
    }

    [Fact]
    public async Task Client_Destination_WireOnly()
    {
        var (hub, wire, _) = CreateHub();
        hub.RegisterEventType(typeof(WireOnlyEvent));
        hub.AddSubscribers(new WireOnlyListener());

        await hub.InvokeAsync(WireOnlyEvent.Shared);

        var sent = Assert.Single(wire);
        Assert.Equal("client_evt", sent.EventId);
        Assert.Same(WireOnlyEvent.Shared, sent.Payload);
    }

    [Fact]
    public async Task Everyone_Destination_ListenersAndWire()
    {
        var (hub, wire, _) = CreateHub();
        hub.RegisterEventType(typeof(EveryoneEvent));
        var listener = new EveryoneListener();
        hub.AddSubscribers(listener);

        await hub.InvokeAsync(new EveryoneEvent("hi"));

        Assert.Equal(1, listener.Count);
        Assert.Equal("every_evt", Assert.Single(wire).EventId);
    }

    [Fact]
    public async Task WireIn_GoesToListenersOnly_NoEcho()
    {
        var (hub, wire, _) = CreateHub();
        hub.RegisterEventType(typeof(ClientReportEvent));
        var gateway = new Gateway(hub);
        var listener = new ReportListener();
        hub.AddSubscribers(listener);

        await gateway.HandleClientEventAsync("report_evt", JsonNode.Parse("""{"message":"hello"}"""));

        Assert.Equal(new[] { "hello" }, listener.Messages);
        Assert.Empty(wire); // wire-in 绝不广播回客户端（无 echo）
    }

    [Fact]
    public async Task WireIn_UnknownEventId_IsIgnored()
    {
        var (hub, wire, _) = CreateHub();
        var gateway = new Gateway(hub);
        var listener = new ReportListener();
        hub.AddSubscribers(listener);

        await gateway.HandleClientEventAsync("no_such_event", JsonNode.Parse("""{"message":"x"}"""));

        Assert.Empty(listener.Messages);
        Assert.Empty(wire);
    }

    [Fact]
    public async Task WireIn_NonReportable_IsIgnored()
    {
        var (hub, wire, _) = CreateHub();
        hub.RegisterEventType(typeof(EveryoneEvent));
        var gateway = new Gateway(hub);
        var listener = new EveryoneListener();
        hub.AddSubscribers(listener);

        await gateway.HandleClientEventAsync("every_evt", JsonNode.Parse("""{"message":"x"}"""));

        Assert.Equal(0, listener.Count);
        Assert.Empty(wire);
    }

    [Theory]
    [InlineData(typeof(BadClientNoId))]
    [InlineData(typeof(BadEveryoneNoId))]
    [InlineData(typeof(BadSinkWithId))]
    public void RegisterEventType_InvalidCombination_FailsFast(Type type)
    {
        var hub = new EventHub();

        Assert.Throws<InvalidOperationException>(() => hub.RegisterEventType(type));
    }

    [Fact]
    public void RegisterEventType_DuplicateEventId_FailsFast()
    {
        var hub = new EventHub();
        hub.RegisterEventType(typeof(DupEventA));

        var ex = Assert.Throws<InvalidOperationException>(() => hub.RegisterEventType(typeof(DupEventB)));
        Assert.Contains("dup_event", ex.Message);
    }

    [Fact]
    public void RegisterEventType_TypeWithoutAttribute_Throws()
    {
        var hub = new EventHub();

        Assert.Throws<ArgumentException>(() => hub.RegisterEventType(typeof(string)));
    }

    [Fact]
    public async Task WireSenderException_DoesNotBlockLocalDispatch()
    {
        var (hub, _, logger) = CreateHub(wireSenderThrows: true);
        hub.RegisterEventType(typeof(EveryoneEvent));
        var listener = new EveryoneListener();
        hub.AddSubscribers(listener);

        await hub.InvokeAsync(new EveryoneEvent("x")); // 不抛出

        Assert.Equal(1, listener.Count);
        Assert.Contains(logger.Messages, m => m.Contains("EveryoneEvent"));
    }

    [Fact]
    public async Task InvokeAsync_RuntimeType_DispatchesByRuntimeType()
    {
        var hub = new EventHub();
        var listener = new LocalListener();
        hub.AddSubscribers(listener);

        await hub.InvokeAsync((object)new LocalEvent());

        Assert.Equal(1, listener.Count);
    }

    private static (EventHub Hub, List<(string EventId, object Payload)> Wire, CollectingLogger Logger) CreateHub(
        bool wireSenderThrows = false)
    {
        var wire = new List<(string, object)>();
        var logger = new CollectingLogger();
        var hub = new EventHub(
            logger,
            wireSender: (id, payload) =>
            {
                if (wireSenderThrows)
                {
                    throw new InvalidOperationException("wire exploded");
                }

                lock (wire)
                {
                    wire.Add((id, payload!));
                }

                return Task.CompletedTask;
            });
        return (hub, wire, logger);
    }

    private sealed class SinkListener : Subscribers
    {
        [Subscribe(typeof(SinkEvent))]
        public Task On(SinkEvent _)
        {
            Assert.Fail("None destination must not reach listeners.");
            return Task.CompletedTask;
        }
    }

    private sealed class LocalListener : Subscribers
    {
        public int Count;

        [Subscribe(typeof(LocalEvent))]
        public Task On(LocalEvent _)
        {
            Interlocked.Increment(ref Count);
            return Task.CompletedTask;
        }
    }

    private sealed class WireOnlyListener : Subscribers
    {
        [Subscribe(typeof(WireOnlyEvent))]
        public Task On(WireOnlyEvent _)
        {
            Assert.Fail("Client destination must not reach listeners.");
            return Task.CompletedTask;
        }
    }

    private sealed class EveryoneListener : Subscribers
    {
        public int Count;

        [Subscribe(typeof(EveryoneEvent))]
        public Task On(EveryoneEvent _)
        {
            Interlocked.Increment(ref Count);
            return Task.CompletedTask;
        }
    }

    private sealed class ReportListener : Subscribers
    {
        private readonly object _gate = new();

        public List<string> Messages
        {
            get
            {
                lock (_gate)
                {
                    return MessagesList.ToList();
                }
            }
        }

        private List<string> MessagesList { get; } = new();

        [Subscribe(typeof(ClientReportEvent))]
        public Task On(ClientReportEvent e)
        {
            lock (_gate)
            {
                MessagesList.Add(e.Message);
            }

            return Task.CompletedTask;
        }
    }

    private sealed class CollectingLogger : Microsoft.Extensions.Logging.ILogger<EventHub>
    {
        public List<string> Messages { get; } = new();

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull
            => null;

        public bool IsEnabled(Microsoft.Extensions.Logging.LogLevel logLevel) => true;

        public void Log<TState>(
            Microsoft.Extensions.Logging.LogLevel logLevel,
            Microsoft.Extensions.Logging.EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            lock (Messages)
            {
                Messages.Add(formatter(state, exception));
            }
        }
    }

    [Publish(Destination = EventDestination.None)]
    private sealed record SinkEvent;

    [Publish(Destination = EventDestination.Listeners)]
    private sealed record LocalEvent;

    [Publish(Id = "client_evt", Destination = EventDestination.Client)]
    private sealed record WireOnlyEvent(string Value)
    {
        public static readonly WireOnlyEvent Shared = new("hi");
    }

    [Publish(Id = "every_evt", Destination = EventDestination.Everyone)]
    private sealed record EveryoneEvent(string Message);

    [Publish(Id = "report_evt", Destination = EventDestination.Listeners)]
    private sealed record ClientReportEvent(string Message);

    [Publish(Destination = EventDestination.Client)]
    private sealed record BadClientNoId;

    [Publish(Destination = EventDestination.Everyone)]
    private sealed record BadEveryoneNoId;

    [Publish(Id = "sink_addr", Destination = EventDestination.None)]
    private sealed record BadSinkWithId;

    [Publish(Id = "dup_event", Destination = EventDestination.Everyone)]
    private sealed record DupEventA;

    [Publish(Id = "dup_event", Destination = EventDestination.Everyone)]
    private sealed record DupEventB;
}
