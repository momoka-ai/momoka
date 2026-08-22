using Momoka.Home.Runtime;
using Newtonsoft.Json;
using Xunit;
using Momoka.Home.Data;
using Momoka.Home;
using Momoka.Home.Levels.Entities;
using Momoka.Home.Data.Sqlite;
using Momoka.Home.Levels;
using Momoka.Home.Levels.Commands;
using Momoka.Home.Runtime.Protocol;
using Momoka.Home.Levels.Volumes;
using Momoka.Home.Primitives;
using Momoka.Home.Levels.Entities.Components;
using Momoka.Home.Levels.Entities.Properties;
namespace Momoka.Home.Tests.Models.Level;

/// <summary>服务器侧：类型化操作、版本号、编辑 token 互斥、装载校验、持久化往返。</summary>
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
        server.Entities.Add(floor);
        server.Session.Layout.Add(floor, new Position(new Float3(0, 0, 0)));
        return server;
    }

    private static T Payload<T>(Result result) =>
        JsonConvert.DeserializeObject<T>(result.Payload!.ToString(), Settings.JsonSerialization)!;

    [Fact]
    public void CreateEntity_FromTemplate_RaisesEntityCreated_AndPoolEntry()
    {
        var server = Server();
        var created = 0;
        server.EntityCreated += _ => created++;

        var result = server.CreateEntity(new CreateEntityRequest { TemplateKey = "momoka:table" }, "c1");
        Assert.True(result.Ok);
        Assert.Equal(1, created);

        var entity = Payload<Entity>(result);
        Assert.Equal(new Key("table"), entity.Key);
        Assert.Contains(server.Entities, e => e.Id == entity.Id);
        Assert.DoesNotContain(server.Session.Layout.Entities, e => e.Id == entity.Id); // 池登记非布局变更
        Assert.Equal(0u, server.Version); // 池登记不产生布局变更版本
    }

    [Fact]
    public void CreateEntity_UnknownTemplate_And_StaleVersion_Fail()
    {
        var server = Server();
        var unknown = server.CreateEntity(new CreateEntityRequest { TemplateKey = "momoka:sofa" }, "c1");
        Assert.False(unknown.Ok);
        Assert.Equal("template_not_found", unknown.ErrorCode);

        var stale = server.CreateEntity(new CreateEntityRequest { TemplateKey = "momoka:table", TemplateVersion = "0" }, "c1");
        Assert.False(stale.Ok);
        Assert.Equal("stale_template_version", stale.ErrorCode);
    }

    [Fact]
    public void Place_OnFloorHost_RaisesLayoutChanged()
    {
        var server = SeededFloorServer();
        var floor = server.Session.Layout.Entities.Single(e => e.Key == new Key("floor"));
        var events = new List<(uint Version, EntityDelta[] Deltas)>();
        server.LayoutChanged += (version, deltas) => events.Add((version, deltas));

        var created = server.CreateEntity(new CreateEntityRequest { TemplateKey = "momoka:table" }, "c1");
        var machine = Payload<Entity>(created);

        var result = server.PlaceEntity(new PlaceEntityRequest
        {
            EntityId = machine.Id,
            Position = new Float3(0, 10, 0),
            HostId = floor.Id,
        }, "c1");

        Assert.True(result.Ok);
        Assert.Equal(1u, result.Version);
        Assert.Contains(server.Session.Layout.Entities, e => e.Id == machine.Id);
        var placedMachine = server.Session.Layout.Find(machine.Id)!;
        Assert.Same(floor.GetComponent<PlacementLayoutSource>(), server.Session.Layout.FindHostEntity(placedMachine));

        var (version, deltas) = Assert.Single(events);
        Assert.Equal(1u, version);
        var delta = Assert.Single(deltas);
        Assert.Equal("added", delta.Kind);
        Assert.Equal(machine.Id, delta.EntityId);
        Assert.NotNull(delta.Entity);
    }

    [Fact]
    public void Place_AlreadyPlaced_And_Collision_Fail()
    {
        var server = SeededFloorServer();
        var created = server.CreateEntity(new CreateEntityRequest { TemplateKey = "momoka:table" }, "c1");
        var machine = Payload<Entity>(created);
        server.PlaceEntity(new PlaceEntityRequest { EntityId = machine.Id, Position = new Float3(0, 10, 0) }, "c1");

        var again = server.PlaceEntity(new PlaceEntityRequest { EntityId = machine.Id, Position = new Float3(10, 10, 0) }, "c1");
        Assert.False(again.Ok);
        Assert.Equal("already_placed", again.ErrorCode);

        var created2 = server.CreateEntity(new CreateEntityRequest { TemplateKey = "momoka:table" }, "c1");
        var other = Payload<Entity>(created2);
        var collision = server.PlaceEntity(new PlaceEntityRequest { EntityId = other.Id, Position = new Float3(0, 10, 0) }, "c1");
        Assert.False(collision.Ok);
        Assert.Equal("invalid_operation", collision.ErrorCode);
    }

    [Fact]
    public void BeginEdit_Mutex_BlocksOtherClients()
    {
        var server = Server();
        Assert.True(server.BeginEdit("a").Ok);

        var denied = server.CreateEntity(new CreateEntityRequest { TemplateKey = "momoka:table" }, "b");
        Assert.False(denied.Ok);
        Assert.Equal("no_edit_token", denied.ErrorCode);

        var held = server.BeginEdit("b");
        Assert.False(held.Ok);
        Assert.Equal("edit_token_held", held.ErrorCode);

        Assert.True(server.CreateEntity(new CreateEntityRequest { TemplateKey = "momoka:table" }, "a").Ok);
        Assert.True(server.EndEdit("a").Ok);
        Assert.True(server.CreateEntity(new CreateEntityRequest { TemplateKey = "momoka:table" }, "b").Ok);
    }

    [Fact]
    public void GetSnapshot_ReturnsFullState()
    {
        var server = SeededFloorServer();
        var created = server.CreateEntity(new CreateEntityRequest { TemplateKey = "momoka:table" }, "c1");
        var machine = Payload<Entity>(created);
        server.PlaceEntity(new PlaceEntityRequest { EntityId = machine.Id, Position = new Float3(0, 10, 0) }, "c1");

        var result = server.GetSnapshot();
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
        var created = server.CreateEntity(new CreateEntityRequest { TemplateKey = "momoka:table" }, "c1");
        var machine = Payload<Entity>(created);
        server.PlaceEntity(new PlaceEntityRequest { EntityId = machine.Id, Position = new Float3(0, 10, 0) }, "c1");

        // 表缺 is_open 属性 → set_property 失败（语义同 SetValue）
        var missing = server.SetProperty(new SetPropertyRequest { EntityId = machine.Id, Name = "is_open", Value = Newtonsoft.Json.Linq.JToken.FromObject(true) }, "c1");
        Assert.False(missing.Ok);

        // 有属性后按 JSON 原生标量设置
        server.Session.Layout.Find(machine.Id)!.AddProperty(new BooleanProperty(Property.IsOpen, false));
        var set = server.SetProperty(new SetPropertyRequest { EntityId = machine.Id, Name = "is_open", Value = Newtonsoft.Json.Linq.JToken.FromObject(true) }, "c1");
        Assert.True(set.Ok);
        Assert.True(server.Session.Layout.Find(machine.Id)!.GetValue<bool>(Property.IsOpen));
    }

    [Fact]
    public void Validate_ReportsDuplicateId_HardError()
    {
        var server = Server();
        var a = Scenes.Box("a", 1, 1, 1);
        server.Entities.Add(a);
        // 构造重复 Id
        server.Entities.Add(new Entity { Id = a.Id, Key = new Key("b"), Volume = new Box3D() });

        var report = server.Validate();
        Assert.Contains(report.HardErrors, e => e.StartsWith("duplicate entity id", StringComparison.Ordinal));
        Assert.False(report.IsValid);
    }

    [Fact]
    public void Load_Save_RoundTrip_PreservesVoxels()
    {
        var root = Path.Combine(Path.GetTempPath(), "momoka_sl_" + Guid.NewGuid().ToString("N"));
        var db = Path.Combine(root, "home.db");

        try
        {
            // 存档生成：ServerLevelData 构造自动创建 Home 实体；Type 经 store.Save 同步到 Home 实体
            var server = new ServerLevelData();
            server.Type = LevelType.Condo;
            using (var store = new SqliteStore(db))
                store.Save(server);

            var server2 = new ServerLevelData();
            server2.Load(new SqliteStore(db));
            Assert.Equal(LevelType.Condo, server2.Type); // 从 Home 实体还原

            var home = Assert.Single(server2.Entities); // 仅 Home 实体（隐藏档案）
            Assert.Equal(LevelData.HomeKey, home.Key);
            Assert.Empty(server2.Layout.Entities); // 空存档：无放置
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }
}
