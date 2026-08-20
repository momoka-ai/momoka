using Newtonsoft.Json;
using Xunit;
using Momoka.Home;
using Momoka.Home.Entities;
using Momoka.Home.Data.Sqlite;
using Momoka.Home.Editing;
using Momoka.Home.Editing.Commands;
using Momoka.Home.Editing.Protocol;
using Momoka.Home.Geometry;
using Momoka.Home.Primitives;
namespace Momoka.Home.Tests.Models.Editing;

/// <summary>服务器侧：请求路由、版本号、编辑 token 互斥、装载校验、三持久化源装载往返。</summary>
public class ServerLevelDataTests
{
    private static ServerLevelData Server()
    {
        var server = new ServerLevelData();
        RegisterTable(server);
        return server;
    }

    private static void RegisterTable(ServerLevelData server) =>
        server.Templates.Register("momoka:table", new EntityTemplate
        {
            Key = new Key("table"),
            Volume = new Box3D { SizeX = 1, SizeY = 1, SizeZ = 1 },
        });

    /// <summary>种子：已放置地板（带 Up 表面）+ 物件模板目录。</summary>
    private static ServerLevelData SeededFloorServer()
    {
        var server = Server();
        RegisterTable(server);
        var floor = Scenes.Floor(5, 1, 5);
        server.Session.Residence.Entities.Add(floor);
        server.Session.Layout.Add(floor, new Position(new Float3(0, 0, 0)));
        return server;
    }

    private static Envelope Req(IRequestFrame request) =>
        Frames.RequestFrame(FrameRegistry.NameOf(request.GetType()), 1, "req-1", request);

    private static T Payload<T>(Result result) =>
        JsonConvert.DeserializeObject<T>(result.Payload!.ToString(), Settings.JsonSerialization)!;

    [Fact]
    public void CreateEntity_FromTemplate_ProducesEntityCreated_AndPoolEntry()
    {
        var server = Server();
        var created = 0;
        server.EntityCreated += (_, _) => created++;

        var result = server.HandleRequest(Req(new CreateEntityRequest { TemplateKey = "momoka:table" }), "c1");
        Assert.True(result.Ok);
        Assert.Equal(1, created);

        var entity = Payload<Entity>(result);
        Assert.Equal(new Key("table"), entity.Key);
        Assert.Contains(server.Session.Residence.Entities, e => e.Id == entity.Id);
        Assert.DoesNotContain(server.Session.Layout.Entities, e => e.Id == entity.Id); // 池登记非布局变更
        Assert.Equal(0u, server.Version); // 池登记不产生布局变更版本
    }

    [Fact]
    public void CreateEntity_UnknownTemplate_And_StaleVersion_Fail()
    {
        var server = Server();
        var unknown = server.HandleRequest(Req(new CreateEntityRequest { TemplateKey = "momoka:sofa" }), "c1");
        Assert.False(unknown.Ok);
        Assert.Equal("template_not_found", unknown.ErrorCode);

        var stale = server.HandleRequest(Req(new CreateEntityRequest { TemplateKey = "momoka:table", TemplateVersion = "0" }), "c1");
        Assert.False(stale.Ok);
        Assert.Equal("stale_template_version", stale.ErrorCode);
    }

    [Fact]
    public void PlaceWashingMachine_OnFloorHost_BroadcastsLayoutChanged()
    {
        var server = SeededFloorServer();
        var floor = server.Session.Layout.Entities.Single(e => e.Key == new Key("floor"));
        var events = new List<LayoutChangedEvent>();
        server.LayoutChanged += (_, e) => events.Add(e.Event);

        var created = server.HandleRequest(Req(new CreateEntityRequest { TemplateKey = "momoka:table" }), "c1");
        var machine = Payload<Entity>(created);

        var result = server.HandleRequest(Req(new PlaceEntityRequest
        {
            EntityId = machine.Id,
            Position = new Float3(0, 10, 0),
            HostId = floor.Id,
        }), "c1");

        Assert.True(result.Ok);
        Assert.Equal(1u, result.Version);
        Assert.Contains(server.Session.Layout.Entities, e => e.Id == machine.Id);
        var placedMachine = server.Session.Layout.Find(machine.Id)!;
        Assert.Same(floor.GetComponent<PlacementLayoutSource>(), server.Session.Layout.FindHostEntity(placedMachine));

        var layoutEvent = Assert.Single(events);
        Assert.Equal(1u, layoutEvent.Version);
        var delta = Assert.Single(layoutEvent.EntityDelta);
        Assert.Equal("added", delta.Kind);
        Assert.Equal(machine.Id, delta.EntityId);
        Assert.NotNull(delta.Entity);
    }

    [Fact]
    public void Place_AlreadyPlaced_And_Collision_Fail()
    {
        var server = SeededFloorServer();
        var created = server.HandleRequest(Req(new CreateEntityRequest { TemplateKey = "momoka:table" }), "c1");
        var machine = Payload<Entity>(created);
        server.HandleRequest(Req(new PlaceEntityRequest { EntityId = machine.Id, Position = new Float3(0, 10, 0) }), "c1");

        var again = server.HandleRequest(Req(new PlaceEntityRequest { EntityId = machine.Id, Position = new Float3(10, 10, 0) }), "c1");
        Assert.False(again.Ok);
        Assert.Equal("already_placed", again.ErrorCode);

        var created2 = server.HandleRequest(Req(new CreateEntityRequest { TemplateKey = "momoka:table" }), "c1");
        var other = Payload<Entity>(created2);
        var collision = server.HandleRequest(Req(new PlaceEntityRequest { EntityId = other.Id, Position = new Float3(0, 10, 0) }), "c1");
        Assert.False(collision.Ok);
        Assert.Equal("invalid_operation", collision.ErrorCode);
    }

    [Fact]
    public void Undo_Redo_RouteThroughServer()
    {
        var server = SeededFloorServer();
        var created = server.HandleRequest(Req(new CreateEntityRequest { TemplateKey = "momoka:table" }), "c1");
        var machine = Payload<Entity>(created);
        server.HandleRequest(Req(new PlaceEntityRequest { EntityId = machine.Id, Position = new Float3(0, 10, 0) }), "c1");
        Assert.Equal(2, server.Session.Layout.Entities.Count);

        var undo = server.HandleRequest(Req(new UndoRequest()), "c1");
        Assert.True(undo.Ok);
        Assert.Equal(2u, undo.Version);
        Assert.DoesNotContain(server.Session.Layout.Entities, e => e.Id == machine.Id);

        var redo = server.HandleRequest(Req(new RedoRequest()), "c1");
        Assert.True(redo.Ok);
        Assert.Equal(3u, redo.Version);
        Assert.Contains(server.Session.Layout.Entities, e => e.Id == machine.Id);
    }

    [Fact]
    public void BeginEdit_Mutex_BlocksOtherClients()
    {
        var server = Server();
        Assert.True(server.HandleRequest(Req(new BeginEditRequest()), "a").Ok);

        var denied = server.HandleRequest(Req(new CreateEntityRequest { TemplateKey = "momoka:table" }), "b");
        Assert.False(denied.Ok);
        Assert.Equal("no_edit_token", denied.ErrorCode);

        var held = server.HandleRequest(Req(new BeginEditRequest()), "b");
        Assert.False(held.Ok);
        Assert.Equal("edit_token_held", held.ErrorCode);

        Assert.True(server.HandleRequest(Req(new CreateEntityRequest { TemplateKey = "momoka:table" }), "a").Ok);
        Assert.True(server.HandleRequest(Req(new EndEditRequest()), "a").Ok);
        Assert.True(server.HandleRequest(Req(new CreateEntityRequest { TemplateKey = "momoka:table" }), "b").Ok);
    }

    [Fact]
    public void GetSnapshot_ReturnsFullState()
    {
        var server = SeededFloorServer();
        var created = server.HandleRequest(Req(new CreateEntityRequest { TemplateKey = "momoka:table" }), "c1");
        var machine = Payload<Entity>(created);
        server.HandleRequest(Req(new PlaceEntityRequest { EntityId = machine.Id, Position = new Float3(0, 10, 0) }), "c1");

        var result = server.HandleRequest(Req(new GetSnapshotRequest()), "c1");
        Assert.True(result.Ok);
        var snapshot = Payload<SnapshotEvent>(result);
        Assert.Contains(snapshot.Entities, e => e.Id == machine.Id);
        Assert.Contains(snapshot.PlacedEntityIds, id => id == machine.Id);
        Assert.Contains(snapshot.TemplateCatalog, t => t.Key == "momoka:table");
        Assert.Equal(1u, snapshot.Version);
    }

    [Fact]
    public void SetProperty_Value_CoercedToPropertyType()
    {
        var server = SeededFloorServer();
        var created = server.HandleRequest(Req(new CreateEntityRequest { TemplateKey = "momoka:table" }), "c1");
        var machine = Payload<Entity>(created);
        server.HandleRequest(Req(new PlaceEntityRequest { EntityId = machine.Id, Position = new Float3(0, 10, 0) }), "c1");

        // 表缺 is_open 属性 → set_property 失败（语义同 SetValue）
        var missing = server.HandleRequest(Req(new SetPropertyRequest { EntityId = machine.Id, Name = "is_open", Value = Newtonsoft.Json.Linq.JToken.FromObject(true) }), "c1");
        Assert.False(missing.Ok);

        // 有属性后按 JSON 原生标量设置
        server.Session.Layout.Find(machine.Id)!.AddProperty(new BooleanProperty(Property.IsOpen, false));
        var set = server.HandleRequest(Req(new SetPropertyRequest { EntityId = machine.Id, Name = "is_open", Value = Newtonsoft.Json.Linq.JToken.FromObject(true) }), "c1");
        Assert.True(set.Ok);
        Assert.True(server.Session.Layout.Find(machine.Id)!.GetValue<bool>(Property.IsOpen));
    }

    [Fact]
    public void Validate_ReportsDuplicateId_HardError()
    {
        var server = Server();
        var a = Scenes.Box("a", 1, 1, 1);
        server.Session.Residence.Entities.Add(a);
        // 构造重复 Id
        server.Session.Residence.Entities.Add(new Entity { Id = a.Id, Key = new Key("b"), Volume = new Box3D() });

        var report = server.Validate();
        Assert.Contains(report.HardErrors, e => e.StartsWith("duplicate entity id", StringComparison.Ordinal));
        Assert.False(report.IsValid);
    }

    [Fact]
    public void Load_Save_RoundTrip_PreservesVoxelsAndRegionNames()
    {
        var root = Path.Combine(Path.GetTempPath(), "momoka_sl_" + Guid.NewGuid().ToString("N"));
        var db = Path.Combine(root, "home.db");
        var chunks = Path.Combine(root, "Chunks");
        var regions = Path.Combine(root, "Regions.json");

        try
        {
            using (var store = new SqliteStore(db))
                store.Save(new Residence { Name = "Test Home", Address = "1 Test St", Type = UnitType.Condo });

            var server = new ServerLevelData();
            server.Load(new SqliteStore(db), chunks, regions);
            RegisterTable(server);

            // 地板直接种（模板管线暂不物化组件）；桌子走协议
            var floor = Scenes.Floor(5, 1, 5);
            server.Session.Residence.Entities.Add(floor);
            server.Session.Layout.Add(floor, new Position(new Float3(0, 0, 0)));
            var created = server.HandleRequest(Req(new CreateEntityRequest { TemplateKey = "momoka:table" }), "c1");
            var machine = Payload<Entity>(created);
            server.HandleRequest(Req(new PlaceEntityRequest { EntityId = machine.Id, Position = new Float3(0, 10, 0) }), "c1");
            server.Session.RebuildRegions();
            var region = server.Session.Regions.Regions!.Cells().First().Value;
            region.Name = "TestRoom";

            Assert.True(server.Save());

            var server2 = new ServerLevelData();
            server2.Load(new SqliteStore(db), chunks, regions);
            Assert.Equal("Test Home", server2.Session.Residence.Name);

            Assert.Equal(2, server2.Session.Layout.Entities.Count);
            var loadedTable = server2.Session.Layout.Find(machine.Id)!;
            Assert.Equal(machine.Id, loadedTable.Id);
            Assert.Same(loadedTable, server2.Session.Layout.Voxels[new Int3(0, 1, 0)]);

            var loadedRegion = server2.Session.Regions.Regions!.Cells().First().Value;
            Assert.Equal(region.Id, loadedRegion.Id); // 持久化 Id 保留
            Assert.Equal("TestRoom", loadedRegion.Name); // 用户命名保留
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }
}
