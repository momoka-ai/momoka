using System.Text.Json.Nodes;
using Xunit;
using Momoka.Core.Events;

namespace Momoka.Core.Tests;

/// <summary>
/// 事件路由（[EventRouter] + EventHub 属性感知分发）：路由矩阵逐项 /
/// FromClients wire-in 无 echo / 组合校验与重复 Id fail-fast / wire-sender 异常不阻断 / 更名兼容。
/// </summary>
public sealed class EventRouterTests
{
    [Fact]
    public async Task None_Destination_OnlyRecords()
    {
        var (hub, wire, recorded) = CreateHub();
        hub.RegisterEventType(typeof(SinkEvent));
        using var listener = hub.Subscribe<SinkEvent>(_ =>
        {
            Assert.Fail("None destination must not reach listeners.");
            return Task.CompletedTask;
        });

        await hub.InvokeAsync(new SinkEvent());

        Assert.Empty(wire);
        Assert.Single(recorded);
    }

    [Fact]
    public async Task Listeners_Destination_LocalOnly()
    {
        var (hub, wire, _) = CreateHub();
        hub.RegisterEventType(typeof(LocalEvent));
        var received = 0;
        using var listener = hub.Subscribe<LocalEvent>(_ =>
        {
            Interlocked.Increment(ref received);
            return Task.CompletedTask;
        });

        await hub.InvokeAsync(new LocalEvent());

        Assert.Equal(1, received);
        Assert.Empty(wire);
    }

    [Fact]
    public async Task Client_Destination_WireOnly()
    {
        var (hub, wire, _) = CreateHub();
        hub.RegisterEventType(typeof(WireOnlyEvent));
        using var listener = hub.Subscribe<WireOnlyEvent>(_ =>
        {
            Assert.Fail("Client destination must not reach listeners.");
            return Task.CompletedTask;
        });

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
        var received = 0;
        using var listener = hub.Subscribe<EveryoneEvent>(_ =>
        {
            Interlocked.Increment(ref received);
            return Task.CompletedTask;
        });

        await hub.InvokeAsync(new EveryoneEvent("hi"));

        Assert.Equal(1, received);
        Assert.Equal("every_evt", Assert.Single(wire).EventId);
    }

    [Fact]
    public async Task FromClients_WireIn_GoesToListenersOnly_NoEcho()
    {
        var (hub, wire, _) = CreateHub();
        hub.RegisterEventType(typeof(ClientReportEvent));
        var gateway = new Gateway(hub);
        var received = new List<string>();
        using var listener = hub.Subscribe<ClientReportEvent>(e =>
        {
            received.Add(e.Message);
            return Task.CompletedTask;
        });

        await gateway.HandleClientEventAsync("report_evt", JsonNode.Parse("""{"message":"hello"}"""));

        Assert.Equal(new[] { "hello" }, received);
        Assert.Empty(wire); // wire-in 绝不广播回客户端（无 echo）
    }

    [Fact]
    public async Task WireIn_UnknownEventId_IsIgnored()
    {
        var (hub, wire, _) = CreateHub();
        var gateway = new Gateway(hub);
        var called = false;
        using var listener = hub.Subscribe<ClientReportEvent>(_ =>
        {
            called = true;
            return Task.CompletedTask;
        });

        await gateway.HandleClientEventAsync("no_such_event", JsonNode.Parse("""{"message":"x"}"""));

        Assert.False(called);
        Assert.Empty(wire);
    }

    [Fact]
    public async Task WireIn_NonFromClients_IsIgnored()
    {
        var (hub, wire, _) = CreateHub();
        hub.RegisterEventType(typeof(EveryoneEvent));
        var gateway = new Gateway(hub);
        var called = false;
        using var listener = hub.Subscribe<EveryoneEvent>(_ =>
        {
            called = true;
            return Task.CompletedTask;
        });

        await gateway.HandleClientEventAsync("every_evt", JsonNode.Parse("""{"message":"x"}"""));

        Assert.False(called);
        Assert.Empty(wire);
    }

    [Theory]
    [InlineData(typeof(BadClientNoId))]
    [InlineData(typeof(BadEveryoneNoId))]
    [InlineData(typeof(BadFromClientsNoId))]
    [InlineData(typeof(BadFromClientsEveryone))]
    [InlineData(typeof(BadListenersWithId))]
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
        var recorder = new List<string>();
        var hub = new EventHub(
            Microsoft.Extensions.Logging.Abstractions.NullLogger<EventHub>.Instance,
            wireSender: (_, _) => throw new InvalidOperationException("wire exploded"),
            recorder: e =>
            {
                recorder.Add(e.GetType().Name);
                return Task.CompletedTask;
            });
        hub.RegisterEventType(typeof(EveryoneEvent));
        var received = 0;
        using var listener = hub.Subscribe<EveryoneEvent>(_ =>
        {
            Interlocked.Increment(ref received);
            return Task.CompletedTask;
        });

        await hub.InvokeAsync(new EveryoneEvent("x")); // 不抛出

        Assert.Equal(1, received);
        Assert.Contains("EveryoneEvent", recorder);
    }

    [Fact]
    public async Task PublishAsync_CompatibilityAlias_StillRoutes()
    {
        var (hub, wire, _) = CreateHub();
        hub.RegisterEventType(typeof(LocalEvent));
        var received = 0;
        using var listener = hub.Subscribe<LocalEvent>(_ =>
        {
            Interlocked.Increment(ref received);
            return Task.CompletedTask;
        });

        await hub.PublishAsync(new LocalEvent());

        Assert.Equal(1, received);
        Assert.Empty(wire);
    }

    [Fact]
    public async Task InvokeAsync_RuntimeType_DispatchesByRuntimeType()
    {
        var hub = new EventHub();
        var received = 0;
        using var listener = hub.Subscribe<LocalEvent>(_ =>
        {
            Interlocked.Increment(ref received);
            return Task.CompletedTask;
        });

        await hub.InvokeAsync((object)new LocalEvent());

        Assert.Equal(1, received);
    }

    private static (EventHub Hub, List<(string EventId, object Payload)> Wire, List<string> Recorded) CreateHub()
    {
        var wire = new List<(string, object)>();
        var recorded = new List<string>();
        var hub = new EventHub(
            Microsoft.Extensions.Logging.Abstractions.NullLogger<EventHub>.Instance,
            wireSender: (id, payload) =>
            {
                lock (wire)
                {
                    wire.Add((id, payload!));
                }

                return Task.CompletedTask;
            },
            recorder: e =>
            {
                lock (recorded)
                {
                    recorded.Add(e.GetType().Name);
                }

                return Task.CompletedTask;
            });
        return (hub, wire, recorded);
    }

    [EventRouter(Destination = EventDestination.None)]
    private sealed record SinkEvent;

    [EventRouter(Destination = EventDestination.Listeners)]
    private sealed record LocalEvent;

    [EventRouter(Id = "client_evt", Destination = EventDestination.Client)]
    private sealed record WireOnlyEvent(string Value)
    {
        public static readonly WireOnlyEvent Shared = new("hi");
    }

    [EventRouter(Id = "every_evt", Destination = EventDestination.Everyone)]
    private sealed record EveryoneEvent(string Message);

    [EventRouter(Id = "report_evt", Destination = EventDestination.Listeners, FromClients = true)]
    private sealed record ClientReportEvent(string Message);

    [EventRouter(Destination = EventDestination.Client)]
    private sealed record BadClientNoId;

    [EventRouter(Destination = EventDestination.Everyone)]
    private sealed record BadEveryoneNoId;

    [EventRouter(FromClients = true)]
    private sealed record BadFromClientsNoId;

    [EventRouter(Id = "bad_combo", Destination = EventDestination.Everyone, FromClients = true)]
    private sealed record BadFromClientsEveryone;

    [EventRouter(Id = "not_needed", Destination = EventDestination.Listeners)]
    private sealed record BadListenersWithId;

    [EventRouter(Id = "dup_event", Destination = EventDestination.Everyone)]
    private sealed record DupEventA;

    [EventRouter(Id = "dup_event", Destination = EventDestination.Everyone)]
    private sealed record DupEventB;
}
