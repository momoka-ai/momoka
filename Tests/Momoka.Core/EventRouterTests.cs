using System.Text.Json.Nodes;
using Xunit;
using Momoka.Core.Behaviors;

namespace Momoka.Core.Tests;

/// <summary>
/// 事件路由与行为管线（[Publish] 可传输契约门 + EventHub 双路分发 / Gateway 行为上报）：
/// 传输门（[Publish] 广播、无属性仅进程内）/ wire-sender 异常不阻断 / Post 执行行为发布事实 /
/// 失败回执 / 行为契约校验 fail-fast。
/// </summary>
public sealed class EventRouterTests
{
    [Fact]
    public async Task Publish_Transmittable_BroadcastsAndDispatchesListeners()
    {
        var (hub, wire, _) = CreateHub();
        var listener = new NotifyListener();
        hub.AddSubscribers(listener);

        await hub.InvokeAsync(new NotifyEvent("hi"));

        Assert.Equal(1, listener.Count);
        var sent = Assert.Single(wire);
        Assert.Equal(typeof(NotifyEvent).FullName!, sent.EventId);
        Assert.Equal("hi", Assert.IsType<NotifyEvent>(sent.Payload).Message);
    }

    [Fact]
    public async Task Publish_NonTransmittable_IsLocalOnly()
    {
        var (hub, wire, _) = CreateHub();
        var listener = new PlainListener();
        hub.AddSubscribers(listener);

        await hub.InvokeAsync(new PlainRecord("x"));

        Assert.Equal(1, listener.Count);
        Assert.Empty(wire);
    }

    [Fact]
    public async Task WireSenderException_DoesNotBlockLocalDispatch()
    {
        var (hub, _, logger) = CreateHub(wireSenderThrows: true);
        var listener = new NotifyListener();
        hub.AddSubscribers(listener);

        await hub.InvokeAsync(new NotifyEvent("x")); // 不抛出

        Assert.Equal(1, listener.Count);
        Assert.Contains(logger.Messages, m => m.Contains("NotifyEvent"));
    }

    [Fact]
    public async Task InvokeAsync_RuntimeType_DispatchesByRuntimeType()
    {
        var hub = new EventHub();
        var listener = new NotifyListener();
        hub.AddSubscribers(listener);

        await hub.InvokeAsync((object)new NotifyEvent("x"));

        Assert.Equal(1, listener.Count);
    }

    [Fact]
    public async Task Post_ExecutesBehaviorAndPublishesFact()
    {
        var (hub, wire, _) = CreateHub();
        var gateway = new Gateway(hub);
        var listener = new GreetListener();
        hub.AddSubscribers(listener);
        gateway.RegisterBehavior(typeof(GreetBehavior));
        var client = TestClient();

        var response = await gateway.HandlePostAsync(
            new GatewayRequest(
                typeof(GreetBehavior.Event).FullName!,
                JsonNode.Parse("""{"message":"hi"}""")),
            client);

        Assert.True(response.Success);
        Assert.Null(response.Error);
        Assert.Equal(1, listener.Count);
        var sent = Assert.Single(wire);
        Assert.Equal(typeof(GreetBehavior.Event).FullName!, sent.EventId);
        Assert.Equal("HI", Assert.IsType<GreetBehavior.Event>(sent.Payload).Message);
    }

    [Fact]
    public async Task Post_UnknownEventId_ReturnsError()
    {
        var (hub, wire, _) = CreateHub();
        var gateway = new Gateway(hub);

        var response = await gateway.HandlePostAsync(
            new GatewayRequest("no.such.event", JsonNode.Parse("{}")),
            TestClient());

        Assert.False(response.Success);
        Assert.Contains("Unknown event", response.Error);
        Assert.Empty(wire);
    }

    [Fact]
    public async Task Post_RequiresPayload_ReturnsError()
    {
        var (hub, _, _) = CreateHub();
        var gateway = new Gateway(hub);
        gateway.RegisterBehavior(typeof(GreetBehavior));

        var response = await gateway.HandlePostAsync(
            new GatewayRequest(typeof(GreetBehavior.Event).FullName!, null),
            TestClient());

        Assert.False(response.Success);
        Assert.Contains("payload", response.Error);
    }

    [Fact]
    public async Task Post_DeserializationFailure_ReturnsError()
    {
        var (hub, wire, _) = CreateHub();
        var gateway = new Gateway(hub);
        gateway.RegisterBehavior(typeof(GreetBehavior));

        var response = await gateway.HandlePostAsync(
            new GatewayRequest(
                typeof(GreetBehavior.Event).FullName!,
                JsonNode.Parse("""{"message":123}""")),
            TestClient());

        Assert.False(response.Success);
        Assert.Contains("Deserialization failed", response.Error);
        Assert.Empty(wire);
    }

    [Fact]
    public async Task Post_ExecuteThrows_ReturnsError()
    {
        var (hub, wire, _) = CreateHub();
        var gateway = new Gateway(hub);
        gateway.RegisterBehavior(typeof(BoomBehavior));

        var response = await gateway.HandlePostAsync(
            new GatewayRequest(
                typeof(BoomBehavior.Event).FullName!,
                JsonNode.Parse("""{"message":"x"}""")),
            TestClient());

        Assert.False(response.Success);
        Assert.Contains("execution exploded", response.Error);
        Assert.Empty(wire);
    }

    [Fact]
    public void RegisterBehavior_NonBehaviorType_Throws()
    {
        var gateway = new Gateway(new EventHub());

        Assert.Throws<ArgumentException>(() => gateway.RegisterBehavior(typeof(PlainRecord)));
    }

    [Fact]
    public void RegisterBehavior_AbstractBehavior_FailsFast()
    {
        var gateway = new Gateway(new EventHub());

        Assert.Throws<InvalidOperationException>(() => gateway.RegisterBehavior(typeof(AbstractBehavior)));
    }

    [Fact]
    public void RegisterBehavior_EventWithoutPublish_FailsFast()
    {
        var gateway = new Gateway(new EventHub());

        var ex = Assert.Throws<ArgumentException>(() => gateway.RegisterBehavior(typeof(UnattributedBehavior)));
        Assert.Contains("[Publish]", ex.Message);
    }

    [Fact]
    public void RegisterBehavior_MissingExecute_FailsFast()
    {
        var gateway = new Gateway(new EventHub());

        var ex = Assert.Throws<ArgumentException>(() => gateway.RegisterBehavior(typeof(MissingExecuteBehavior)));
        Assert.Contains("Execute", ex.Message);
    }

    [Fact]
    public void RegisterBehavior_DuplicateEventId_FailsFast()
    {
        var gateway = new Gateway(new EventHub());
        gateway.RegisterBehavior(typeof(GreetBehavior));

        var ex = Assert.Throws<InvalidOperationException>(() => gateway.RegisterBehavior(typeof(GreetBehavior)));
        Assert.Contains("already registered", ex.Message);
    }

    private static Client TestClient() => new("conn-1", "ui-1", "user", DateTimeOffset.UtcNow);

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

    private sealed class NotifyListener : Subscribers
    {
        public int Count;

        [Subscribe(typeof(NotifyEvent))]
        public Task On(NotifyEvent _)
        {
            Interlocked.Increment(ref Count);
            return Task.CompletedTask;
        }
    }

    private sealed class PlainListener : Subscribers
    {
        public int Count;

        [Subscribe(typeof(PlainRecord))]
        public Task On(PlainRecord _)
        {
            Interlocked.Increment(ref Count);
            return Task.CompletedTask;
        }
    }

    private sealed class GreetListener : Subscribers
    {
        public int Count;

        [Subscribe(typeof(GreetBehavior.Event))]
        public Task On(GreetBehavior.Event _)
        {
            Interlocked.Increment(ref Count);
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

    [Publish]
    private sealed record NotifyEvent(string Message);

    private sealed record PlainRecord(string Value);

    private sealed class GreetBehavior : Behavior<GreetBehavior>
    {
        [Publish]
        public sealed record Event(string Message);

        public sealed record Intent(string Message);

        public Event Execute(Intent intent, IntentSource? source = null)
            => new(intent.Message.ToUpperInvariant());
    }

    private sealed class BoomBehavior : Behavior<BoomBehavior>
    {
        [Publish]
        public sealed record Event(string Message);

        public sealed record Intent(string Message);

        public Event Execute(Intent intent, IntentSource? source = null)
            => throw new InvalidOperationException("execution exploded");
    }

    private sealed class UnattributedBehavior : Behavior<UnattributedBehavior>
    {
        public sealed record Event(string Message);

        public sealed record Intent(string Message);

        public Event Execute(Intent intent, IntentSource? source = null)
            => new(intent.Message);
    }

    private sealed class MissingExecuteBehavior : Behavior<MissingExecuteBehavior>
    {
        [Publish]
        public sealed record Event(string Message);

        public sealed record Intent(string Message);
    }

    private abstract class AbstractBehavior : Behavior<AbstractBehavior>
    {
        [Publish]
        public sealed record Event(string Message);

        public sealed record Intent(string Message);
    }
}
