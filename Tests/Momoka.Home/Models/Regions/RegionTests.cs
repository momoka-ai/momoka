using Xunit;
using Momoka.Home;
using Momoka.Home.Entities;
using Momoka.Home.Geometry;
using Momoka.Home.Layouts;
using Momoka.Home.Primitives;
using Momoka.Home.Entities.Components;
using Momoka.Home.Entities.Properties;
namespace Momoka.Home.Tests.Models.Regions;

/// <summary>
/// Region.BuildLayout derives the region layer from occupancy + placement
/// surfaces: standing cells come from Up-facing structural placement planes;
/// structural-and-closed entities block (walls, closed doors); open portals
/// pass; non-structural furniture neither seeds nor blocks.
/// </summary>
public class RegionTests
{
    private static Entity Box(string path, int sx, int sy, int sz) => new()
    {
        Key = new Key(path),
        Volume = new Box3D { SizeX = sx, SizeY = sy, SizeZ = sz },
    };

    /// <summary>结构件盒子：is_structural 标记（墙 / 天花板 / 门）。</summary>
    private static Entity StructuralBox(string path, int sx, int sy, int sz)
    {
        var entity = Box(path, sx, sy, sz);
        entity.AddProperties(new[] { new BooleanProperty(Property.IsImmutable, true) });
        return entity;
    }

    /// <summary>结构件盒子：顶面放置面（Up，Transform 位置在 surfaceY）+ is_structural。</summary>
    private static Entity SurfaceBox(string path, int sx, int sy, int sz, Int3 pos, int surfaceY)
    {
        var entity = StructuralBox(path, sx, sy, sz);
        var surface = new GridLayout<bool>(new Int2(sx, sz));
        surface.Fill(true, Int2.Zero, new Int2(sx, sz));
        entity.AddComponent(new PlacementLayoutSource
        {
            Layout = surface,
            Transform = new Transform(new Float3(pos.X * 10, surfaceY * 10, pos.Z * 10), Rotation.Up),
        });
        return entity;
    }

    /// <summary>
    /// 10×9×30 封闭空间；中墙 (x=5, 全高) 分左右两室。墙体错位角接避免重叠。
    /// 左室 x=1..4、右室 x=6..8，z=1..8，span [1,30)。
    /// </summary>
    private static UnitLayout TwoRoomScene()
    {
        var l = new UnitLayout();
        l.Add(SurfaceBox("floor", 10, 1, 10, new Int3(0, 0, 0), 1), new Position(new Float3(0, 0, 0)));
        l.Add(StructuralBox("ceiling", 10, 1, 10), new Position(new Float3(0, 300, 0)));
        l.Add(StructuralBox("wall", 10, 29, 1), new Position(new Float3(0, 10, 0))); // 北 z=0
        l.Add(StructuralBox("wall", 10, 29, 1), new Position(new Float3(0, 10, 90))); // 南 z=9
        l.Add(StructuralBox("wall", 1, 29, 8), new Position(new Float3(0, 10, 10)));  // 西 x=0
        l.Add(StructuralBox("wall", 1, 29, 8), new Position(new Float3(90, 10, 10)));  // 东 x=9
        l.Add(StructuralBox("wall", 1, 29, 8), new Position(new Float3(50, 10, 10)));  // 中 x=5
        return l;
    }

    private static List<Region> DistinctRegions(ColumnLayout<Region> map) =>
        map.Cells().Select(c => c.Value).Distinct().ToList();

    [Fact]
    public void BuildLayout_SplitsByFullHeightWall()
    {
        var map = Region.BuildLayout(TwoRoomScene());

        var left = map.At(2, 5, 2);
        var right = map.At(7, 5, 2);
        Assert.NotNull(left);
        Assert.NotNull(right);
        Assert.NotEqual(left!.Id, right!.Id);

        Assert.Null(map.At(5, 5, 2));  // 中墙
        Assert.Null(map.At(2, 0, 2));  // 地板下
        Assert.Null(map.At(2, 30, 2)); // 天花板

        var regions = DistinctRegions(map);
        Assert.Equal(2, regions.Count);
        var leftRegion = regions.Single(r => r.Area == 32);
        var rightRegion = regions.Single(r => r.Area == 24);
        Assert.Equal(928, leftRegion.Volume); // 4×8×29
        Assert.Equal(696, rightRegion.Volume); // 3×8×29
    }

    [Fact]
    public void BuildLayout_MergesThroughDoorway()
    {
        // 中墙留门洞 z=4：两段 [1..3] 与 [5..8]
        var l = new UnitLayout();
        l.Add(SurfaceBox("floor", 10, 1, 10, new Int3(0, 0, 0), 1), new Position(new Float3(0, 0, 0)));
        l.Add(StructuralBox("ceiling", 10, 1, 10), new Position(new Float3(0, 300, 0)));
        l.Add(StructuralBox("wall", 10, 29, 1), new Position(new Float3(0, 10, 0)));
        l.Add(StructuralBox("wall", 10, 29, 1), new Position(new Float3(0, 10, 90)));
        l.Add(StructuralBox("wall", 1, 29, 8), new Position(new Float3(0, 10, 10)));
        l.Add(StructuralBox("wall", 1, 29, 8), new Position(new Float3(90, 10, 10)));
        l.Add(StructuralBox("wall", 1, 29, 3), new Position(new Float3(50, 10, 10))); // 中墙 z=1..3
        l.Add(StructuralBox("wall", 1, 29, 4), new Position(new Float3(50, 10, 50))); // 中墙 z=5..8

        var map = Region.BuildLayout(l);
        var regions = DistinctRegions(map);
        var room = Assert.Single(regions);

        Assert.Same(room, map.At(2, 5, 2));
        Assert.Same(room, map.At(7, 5, 2)); // 门洞连通
        Assert.Equal(928 + 696 + 29, room.Volume);
        Assert.Equal(32 + 24 + 1, room.Area);
    }

    [Fact]
    public void BuildLayout_AgentClimbHeightControlsConnectivity()
    {
        // 相邻两列：A span [1,20)，B span [21,40)，间距 1
        var l = new UnitLayout();
        l.Add(SurfaceBox("floor", 1, 1, 1, new Int3(0, 0, 0), 1), new Position(new Float3(0, 0, 0)));   // A 地板面 y=1
        l.Add(StructuralBox("ceiling", 1, 1, 1), new Position(new Float3(0, 200, 0)));                 // A 天花 y=20
        l.Add(StructuralBox("base", 1, 20, 1), new Position(new Float3(10, 0, 0)));                    // B 基座 y=0..19
        l.Add(SurfaceBox("floor", 1, 1, 1, new Int3(1, 20, 0), 21), new Position(new Float3(10, 200, 0))); // B 地板面 y=21
        l.Add(StructuralBox("ceiling", 1, 1, 1), new Position(new Float3(10, 400, 0)));                // B 天花 y=40

        var merged = Region.BuildLayout(l, Agent.Human with { MaxClimbHeight = 1 });
        Assert.Single(DistinctRegions(merged));

        var split = Region.BuildLayout(l, Agent.Human with { MaxClimbHeight = 0 });
        Assert.Equal(2, DistinctRegions(split).Count);
    }

    [Fact]
    public void BuildLayout_NonStructuralFurniture_DoesNotBlock()
    {
        var l = new UnitLayout();
        l.Add(SurfaceBox("floor", 5, 1, 5, new Int3(0, 0, 0), 1), new Position(new Float3(0, 0, 0)));
        l.Add(StructuralBox("ceiling", 5, 1, 5), new Position(new Float3(0, 300, 0)));
        l.Add(StructuralBox("wall", 5, 29, 1), new Position(new Float3(0, 10, 0))); // 北 z=0
        l.Add(StructuralBox("wall", 5, 29, 1), new Position(new Float3(0, 10, 40))); // 南 z=4
        l.Add(StructuralBox("wall", 1, 29, 3), new Position(new Float3(0, 10, 10))); // 西 x=0
        l.Add(StructuralBox("wall", 1, 29, 3), new Position(new Float3(40, 10, 10))); // 东 x=4
        l.Add(Box("shelf", 1, 20, 1), new Position(new Float3(20, 10, 20))); // 中央高书架（非结构 → 不阻断）

        var map = Region.BuildLayout(l);
        var room = Assert.Single(DistinctRegions(map));

        Assert.Same(room, map.At(2, 10, 2)); // 书架格不再是洞
        Assert.Same(room, map.At(1, 5, 2));
        Assert.Same(map.At(1, 5, 2), map.At(3, 5, 2));
    }

    [Fact]
    public void BuildLayout_StructuralColumn_MakesHole()
    {
        var l = new UnitLayout();
        l.Add(SurfaceBox("floor", 5, 1, 5, new Int3(0, 0, 0), 1), new Position(new Float3(0, 0, 0)));
        l.Add(StructuralBox("ceiling", 5, 1, 5), new Position(new Float3(0, 300, 0)));
        l.Add(StructuralBox("wall", 5, 29, 1), new Position(new Float3(0, 10, 0))); // 北 z=0
        l.Add(StructuralBox("wall", 5, 29, 1), new Position(new Float3(0, 10, 40))); // 南 z=4
        l.Add(StructuralBox("wall", 1, 29, 3), new Position(new Float3(0, 10, 10))); // 西 x=0
        l.Add(StructuralBox("wall", 1, 29, 3), new Position(new Float3(40, 10, 10))); // 东 x=4
        l.Add(StructuralBox("column", 1, 29, 1), new Position(new Float3(20, 10, 20))); // 结构柱

        var map = Region.BuildLayout(l);
        var room = Assert.Single(DistinctRegions(map));

        Assert.Null(map.At(2, 10, 2)); // 结构柱 = 洞
        Assert.Same(room, map.At(1, 5, 2));
        Assert.Same(room, map.At(3, 5, 2));
    }

    [Fact]
    public void BuildLayout_NonStructuralSurface_DoesNotSeedRegions()
    {
        // 地板（structural）+ 一张非 structural 的桌子（有 Up 顶面 y=8）
        var l = new UnitLayout();
        l.Add(SurfaceBox("floor", 5, 1, 5, new Int3(0, 0, 0), 1), new Position(new Float3(0, 0, 0)));
        l.Add(StructuralBox("ceiling", 5, 1, 5), new Position(new Float3(0, 300, 0)));
        var table = Box("table", 3, 1, 3);
        var tableSurface = new GridLayout<bool>(new Int2(3, 3));
        tableSurface.Fill(true, Int2.Zero, new Int2(3, 3));
        table.AddComponent(new PlacementLayoutSource
        {
            Layout = tableSurface,
            Transform = new Transform(new Float3(10, 80, 10), Rotation.Up),
        });
        l.Add(table, new Position(new Float3(10, 70, 10))); // 桌面 y=7 占用，顶面 y=8

        var map = Region.BuildLayout(l);
        var room = Assert.Single(DistinctRegions(map));

        Assert.Same(room, map.At(2, 8, 2));    // 桌顶不产新站立格（非 structural），仍是同一区域
        Assert.Same(room, map.At(2, 7, 2));    // 桌体非结构，不切断 span
        Assert.Same(room, map.At(2, 5, 2));    // 桌下 [1,30) 仍是区域
    }

    [Fact]
    public void BuildLayout_DoorClosed_Splits_Open_Merges()
    {
        // 中墙留门洞 z=4：两段 [1..3] 与 [5..8]，门实体填洞
        var l = new UnitLayout();
        l.Add(SurfaceBox("floor", 10, 1, 10, new Int3(0, 0, 0), 1), new Position(new Float3(0, 0, 0)));
        l.Add(StructuralBox("ceiling", 10, 1, 10), new Position(new Float3(0, 300, 0)));
        l.Add(StructuralBox("wall", 10, 29, 1), new Position(new Float3(0, 10, 0)));
        l.Add(StructuralBox("wall", 10, 29, 1), new Position(new Float3(0, 10, 90)));
        l.Add(StructuralBox("wall", 1, 29, 8), new Position(new Float3(0, 10, 10)));
        l.Add(StructuralBox("wall", 1, 29, 8), new Position(new Float3(90, 10, 10)));
        l.Add(StructuralBox("wall", 1, 29, 3), new Position(new Float3(50, 10, 10))); // 中墙 z=1..3
        l.Add(StructuralBox("wall", 1, 29, 4), new Position(new Float3(50, 10, 50))); // 中墙 z=5..8
        var door = StructuralBox("door", 1, 29, 1);
        door.AddProperties(new[] { new BooleanProperty(Property.IsOpen, false) });
        l.Add(door, new Position(new Float3(50, 10, 40))); // 门实体占门洞 z=4

        // 关门：两室分离
        var closed = Region.BuildLayout(l);
        Assert.Equal(2, DistinctRegions(closed).Count);
        Assert.NotEqual(closed.At(2, 5, 2)!.Id, closed.At(7, 5, 2)!.Id);

        // 开门：两室连通
        door.SetValue(Property.IsOpen, true);
        var opened = Region.BuildLayout(l);
        var room = Assert.Single(DistinctRegions(opened));
        Assert.Same(room, opened.At(2, 5, 2));
        Assert.Same(room, opened.At(7, 5, 2));
    }

    [Fact]
    public void BuildLayout_EmptyLayout_HasNoRegions()
    {
        var map = Region.BuildLayout(new UnitLayout());
        Assert.Null(map.At(0, 0, 0));
        Assert.Empty(map.Cells());
    }

    [Fact]
    public void Region_Name_DefaultsAndIsSettable()
    {
        var map = Region.BuildLayout(TwoRoomScene());
        var left = map.At(2, 5, 2)!;
        Assert.False(string.IsNullOrEmpty(left.Name)); // 默认 "Region {Id}"

        left.Name = "Bedroom";
        Assert.Equal("Bedroom", left.Name);
        Assert.Same(left, map.At(2, 5, 2)); // 共享引用，改一处全列生效
    }
}
