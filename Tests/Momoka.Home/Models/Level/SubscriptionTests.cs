using Xunit;
using Momoka.Home;
using Momoka.Home.Data.Sqlite;
using Momoka.Home.Level;
using Momoka.Home.Level.Protocol;
using Momoka.Home.Entities;
using Momoka.Home.Geometry;
using Momoka.Home.Primitives;
namespace Momoka.Home.Tests.Models.Level;

/// <summary>Pub/Sub 内存订阅：按 topic 订阅 / 退订 / 广播（宿主 WS fan-out 单列后续阶段）。</summary>
public class SubscriptionTests
{
    private sealed class Collector : ISubscriber
    {
        public List<Envelope> Frames { get; } = new();
        public void OnFrame(Envelope envelope) => Frames.Add(envelope);
    }

    private static ServerLevelData Seeded()
    {
        var server = new ServerLevelData();
        server.Templates.Register("momoka:table", new EntityTemplate
        {
            Key = new Key("table"),
            Volume = new Box3D { SizeX = 1, SizeY = 1, SizeZ = 1 },
        });
        var floor = Scenes.Floor(5, 1, 5);
        server.Entities.Add(floor);
        server.Session.Layout.Add(floor, new Position(new Float3(0, 0, 0)));
        return server;
    }

    private static Envelope Req(IRequestFrame request) =>
        Frames.RequestFrame(FrameRegistry.NameOf(request.GetType()), 1, "req-1", request);

    [Fact]
    public void Subscribe_ReceivesTopicEvents_UnsubscribeStops()
    {
        var server = Seeded();
        var layout = new Collector();
        var lifecycle = new Collector();
        server.Subscribe(Topics.Layout, layout);
        server.Subscribe(Topics.Lifecycle, lifecycle);

        // 创建实体 → entities topic（无人订阅）；放置 → layout topic
        var created = server.HandleRequest(Req(new CreateEntityRequest { TemplateKey = "momoka:table" }), "c1");
        var machine = (Entity)Newtonsoft.Json.JsonConvert.DeserializeObject<Entity>(
            created.Payload!.ToString(), Settings.JsonSerialization)!;
        server.HandleRequest(Req(new PlaceEntityRequest { EntityId = machine.Id, Position = new Float3(0, 10, 0) }), "c1");

        var layoutFrame = Assert.Single(layout.Frames);
        Assert.Equal("layout_changed", layoutFrame.Type);
        Assert.Equal("req-1", layoutFrame.RequestId);
        Assert.Empty(lifecycle.Frames);

        // 退订后不再接收
        server.Unsubscribe(Topics.Layout, layout);
        var created2 = server.HandleRequest(Req(new CreateEntityRequest { TemplateKey = "momoka:table" }), "c1");
        var other = (Entity)Newtonsoft.Json.JsonConvert.DeserializeObject<Entity>(
            created2.Payload!.ToString(), Settings.JsonSerialization)!;
        server.HandleRequest(Req(new PlaceEntityRequest { EntityId = other.Id, Position = new Float3(10, 10, 0) }), "c1");
        Assert.Single(layout.Frames);
    }

    [Fact]
    public void FailedRequest_BroadcastsError_OnLifecycle()
    {
        var server = Seeded();
        var lifecycle = new Collector();
        server.Subscribe(Topics.Lifecycle, lifecycle);

        var result = server.HandleRequest(Req(new CreateEntityRequest { TemplateKey = "momoka:nope" }), "c1");
        Assert.False(result.Ok);

        var errorFrame = Assert.Single(lifecycle.Frames);
        Assert.Equal("error", errorFrame.Type);
        var error = (ErrorEvent)FrameRegistry.CreateEvent(errorFrame.Type, errorFrame.Payload);
        Assert.Equal("template_not_found", error.ErrorCode);
    }

    [Fact]
    public void Save_BroadcastsSaveCompleted()
    {
        var root = Path.Combine(Path.GetTempPath(), "momoka_sub_" + Guid.NewGuid().ToString("N"));
        var db = Path.Combine(root, "home.db");
        var chunks = Path.Combine(root, "Chunks");
        try
        {
            var seed = new ServerLevelData { Type = UnitType.House }; // 构造自动创建 Home 实体
            using (var store = new SqliteStore(db))
                store.Save(seed);
            var server = new ServerLevelData();
            server.Load(new SqliteStore(db));

            var lifecycle = new Collector();
            server.Subscribe(Topics.Lifecycle, lifecycle);
            Assert.True(server.HandleRequest(Req(new SaveRequest()), "c1").Ok);
            Assert.Equal("save_completed", Assert.Single(lifecycle.Frames).Type);
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }
}
