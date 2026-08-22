using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xunit;
using Momoka.Home.Data;
using Momoka.Home.Levels;
using Momoka.Home.Levels.Entities;
using Momoka.Home.Levels.Entities.Components;
using Momoka.Home.Levels.Entities.Properties;
using Momoka.Home.Levels.Layouts;
using Momoka.Home.Levels.Volumes;
using Momoka.Home.Primitives;
using Momoka.Home.Runtime;
using Momoka.Home.Runtime.Protocol;
namespace Momoka.Home.Tests.Models.Layouts;

/// <summary>
/// ChildrenSource 容器关系：Id 持久化、装载重链、表面挂载继承、客户端 null 保护。
/// </summary>
public class ChildrenSourceTests
{
    private static Entity Box(string key, int sx, int sy, int sz) => new()
    {
        Key = new Key(key),
        Volume = new Box { SizeX = sx, SizeY = sy, SizeZ = sz },
    };

    private static Entity Floor()
    {
        var floor = Box("floor", 5, 1, 5);
        var surface = new GridLayout<bool>(new Int2(5, 5));
        surface.Fill(true, Int2.Zero, new Int2(5, 5));
        floor.AddComponent(new PlacementLayoutSource
        {
            Layout = surface,
            Transform = new Transform(new Float3(0, 10, 0), Rotation.Up),
        });
        return floor;
    }

    [Fact]
    public void PlacementLayoutSource_IsA_ChildrenSource()
    {
        Assert.IsAssignableFrom<ChildrenSource>(new PlacementLayoutSource());
    }

    [Fact]
    public void ChildrenSource_Serializes_AsIdList_NotInlineEntities()
    {
        var container = new Entity { Key = new Key("wall_system") };
        var group = new ChildrenSource();
        container.AddComponent(group);
        var wall = Box("wall", 2, 29, 1);
        group.Children.Add(wall);

        var json = JsonConvert.SerializeObject(container, Settings.JsonSerialization);
        var children = (JArray)JObject.Parse(json)["components"]![0]!["children"]!;
        Assert.Equal(new[] { wall.Id.ToString() }, children.Select(t => t.Value<string>()));

        var back = JsonConvert.DeserializeObject<Entity>(json, Settings.JsonSerialization)!;
        var restored = Assert.IsType<ChildrenSource>(Assert.Single(back.Components));
        Assert.Equal(new[] { wall.Id }, restored.Children.Select(c => c.Id)); // 反序列化 id-stub
    }

    [Fact]
    public void RestorePlacementFromGrid_ReLinks_GroupMembers_ByStoredIds()
    {
        var container = new Entity { Key = new Key("wall_system") };
        var group = new ChildrenSource();
        container.AddComponent(group);
        var wallA = Box("wall", 2, 29, 1);
        var wallB = Box("wall", 2, 10, 1); // 矮墙——独立实体自带高度
        group.Children.Add(wallA);
        group.Children.Add(wallB);

        var layout = new LevelLayout();
        layout.RestorePlacementFromGrid(new[] { container, wallA, wallB });

        Assert.Equal(2, group.Children.Count);
        Assert.Contains(wallA, group.Children);
        Assert.Contains(wallB, group.Children);
        Assert.Null(layout.FindHostEntity(wallA)); // 组挂载非表面 → null
    }

    [Fact]
    public void SurfacePlacement_RegistersInto_ChildrenAndIds()
    {
        var layout = new LevelLayout();
        var floor = Floor();
        Assert.True(layout.Add(floor, new Position(new Float3(0, 0, 0))));
        var surface = floor.GetComponent<PlacementLayoutSource>()!;

        var mug = Box("mug", 1, 1, 1);
        Assert.True(layout.Add(mug, new Position(new Float3(0, 10, 0)), surface));

        Assert.Contains(mug, surface.Children);
        Assert.Contains(mug.Id, surface.ChildrenIds);
        Assert.Same(surface, layout.FindHostEntity(mug));
    }

    [Fact]
    public void SurfaceHost_RoundTrips_ThroughSerializedIds()
    {
        var layout = new LevelLayout();
        var floor = Floor();
        layout.Add(floor, new Position(new Float3(0, 0, 0)));
        var surface = floor.GetComponent<PlacementLayoutSource>()!;
        var mug = Box("mug", 1, 1, 1);
        layout.Add(mug, new Position(new Float3(0, 10, 0)), surface);

        var json = JsonConvert.SerializeObject(new[] { floor, mug }, Settings.JsonSerialization);
        var back = JsonConvert.DeserializeObject<Entity[]>(json, Settings.JsonSerialization)!;

        var restoredLayout = new LevelLayout();
        restoredLayout.RestorePlacementFromGrid(back);

        var restoredFloor = back.Single(e => e.Key == new Key("floor"));
        var restoredMug = back.Single(e => e.Key == new Key("mug"));
        var restoredSurface = Assert.IsType<PlacementLayoutSource>(Assert.Single(restoredFloor.Components));
        Assert.Contains(restoredMug, restoredSurface.Children);
        Assert.Same(restoredSurface, restoredLayout.FindHostEntity(restoredMug));
    }

    [Fact]
    public void ClientMirror_SkipsNullVolume_ContainerEntities()
    {
        var container = new Entity { Key = new Key("wall_system") }; // Volume = null（marker）

        var snapshot = new SnapshotEvent
        {
            Type = "estate",
            Entities = new[] { container },
            PlacedEntityIds = Array.Empty<Guid>(),
            TemplateCatalog = Array.Empty<TemplateCatalogEntry>(),
            Version = 1,
        };
        var client = new ClientLevelData();
        client.ApplySnapshot(snapshot); // 不应抛 NRE

        var deltas = new[]
        {
            new EntityDelta { Kind = "added", EntityId = container.Id, Entity = container },
        };
        client.Apply(deltas, 2); // 写格保护：null 体积跳过
        Assert.Contains(container.Id, client.Registry.Keys);
        Assert.Contains(container, client.Placed);
    }
}
