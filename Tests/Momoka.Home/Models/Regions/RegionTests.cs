using Xunit;
using Momoka.Home;
using Momoka.Home.Components;
using Momoka.Home.Entities;
using Momoka.Home.Geometry;
using Momoka.Home.Layouts;
using Momoka.Home.Primitives;
using Momoka.Home.Properties;
namespace Momoka.Home.Tests.Models.Regions;

/// <summary>
/// Region.BuildLayout derives the region layer from occupancy + placement
/// surfaces: standing cells come from Up-facing VoxelLayoutSource planes with
/// headroom; walls block by occupancy; furniture becomes harmless holes.
/// </summary>
public class RegionTests
{
    private static Entity Box(string path, int sx, int sy, int sz) => new()
    {
        Key = new Key(path),
        Volume = new Box3D { SizeX = sx, SizeY = sy, SizeZ = sz },
    };

    /// <summary>结构件盒子：顶面放置面（Up，Offset 在 surfaceY）+ is_structural。</summary>
    private static Entity SurfaceBox(string path, int sx, int sy, int sz, Int3 pos, int surfaceY)
    {
        var entity = Box(path, sx, sy, sz);
        entity.AddProperties(new[] { new BooleanProperty(BuiltinProperty.IS_STRUCTURAL.Name, true) });
        var surface = new GridLayout<bool>(new Int2(sx, sz), new Int3(pos.X, surfaceY, pos.Z));
        surface.Fill(true, Int2.Zero, new Int2(sx, sz));
        entity.AddComponent(new PlacementLayoutSource { Layout = surface });
        return entity;
    }

    /// <summary>
    /// 10×9×30 封闭空间；中墙 (x=5, 全高) 分左右两室。墙体错位角接避免重叠。
    /// 左室 x=1..4、右室 x=6..8，z=1..8，span [1,30)。
    /// </summary>
    private static VoxelLayout<Entity> TwoRoomScene()
    {
        var l = new VoxelLayout<Entity>();
        l.BuildAt(SurfaceBox("floor", 10, 1, 10, new Int3(0, 0, 0), 1), new Int3(0, 0, 0));
        l.BuildAt(Box("ceiling", 10, 1, 10), new Int3(0, 30, 0));
        l.BuildAt(Box("wall", 10, 29, 1), new Int3(0, 1, 0)); // 北 z=0
        l.BuildAt(Box("wall", 10, 29, 1), new Int3(0, 1, 9)); // 南 z=9
        l.BuildAt(Box("wall", 1, 29, 8), new Int3(0, 1, 1));  // 西 x=0
        l.BuildAt(Box("wall", 1, 29, 8), new Int3(9, 1, 1));  // 东 x=9
        l.BuildAt(Box("wall", 1, 29, 8), new Int3(5, 1, 1));  // 中 x=5
        return l;
    }

    private static List<Region> DistinctRegions(ColumnLayout<Region> map) =>
        map.AllSpans().Select(s => s.Span.Value).Distinct().ToList();

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
        var l = new VoxelLayout<Entity>();
        l.BuildAt(SurfaceBox("floor", 10, 1, 10, new Int3(0, 0, 0), 1), new Int3(0, 0, 0));
        l.BuildAt(Box("ceiling", 10, 1, 10), new Int3(0, 30, 0));
        l.BuildAt(Box("wall", 10, 29, 1), new Int3(0, 1, 0));
        l.BuildAt(Box("wall", 10, 29, 1), new Int3(0, 1, 9));
        l.BuildAt(Box("wall", 1, 29, 8), new Int3(0, 1, 1));
        l.BuildAt(Box("wall", 1, 29, 8), new Int3(9, 1, 1));
        l.BuildAt(Box("wall", 1, 29, 3), new Int3(5, 1, 1)); // 中墙 z=1..3
        l.BuildAt(Box("wall", 1, 29, 4), new Int3(5, 1, 5)); // 中墙 z=5..8

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
        var l = new VoxelLayout<Entity>();
        l.BuildAt(SurfaceBox("floor", 1, 1, 1, new Int3(0, 0, 0), 1), new Int3(0, 0, 0));   // A 地板面 y=1
        l.BuildAt(Box("ceiling", 1, 1, 1), new Int3(0, 20, 0));                             // A 天花 y=20
        l.BuildAt(Box("base", 1, 20, 1), new Int3(1, 0, 0));                                 // B 基座 y=0..19
        l.BuildAt(SurfaceBox("floor", 1, 1, 1, new Int3(1, 20, 0), 21), new Int3(1, 20, 0)); // B 地板面 y=21
        l.BuildAt(Box("ceiling", 1, 1, 1), new Int3(1, 40, 0));                              // B 天花 y=40

        var merged = Region.BuildLayout(l, new Agent(MaxClimbHeight: 1));
        Assert.Single(DistinctRegions(merged));

        var split = Region.BuildLayout(l, new Agent(MaxClimbHeight: 0));
        Assert.Equal(2, DistinctRegions(split).Count);
    }

    [Fact]
    public void BuildLayout_FurnitureBecomesHole()
    {
        var l = new VoxelLayout<Entity>();
        l.BuildAt(SurfaceBox("floor", 5, 1, 5, new Int3(0, 0, 0), 1), new Int3(0, 0, 0));
        l.BuildAt(Box("ceiling", 5, 1, 5), new Int3(0, 30, 0));
        l.BuildAt(Box("wall", 5, 29, 1), new Int3(0, 1, 0)); // 北 z=0
        l.BuildAt(Box("wall", 5, 29, 1), new Int3(0, 1, 4)); // 南 z=4
        l.BuildAt(Box("wall", 1, 29, 3), new Int3(0, 1, 1)); // 西 x=0
        l.BuildAt(Box("wall", 1, 29, 3), new Int3(4, 1, 1)); // 东 x=4
        l.BuildAt(Box("shelf", 1, 20, 1), new Int3(2, 1, 2)); // 中央高书架（无 surface）

        var map = Region.BuildLayout(l);
        var room = Assert.Single(DistinctRegions(map));

        Assert.Null(map.At(2, 10, 2));              // 书架格 = 洞
        Assert.Same(room, map.At(1, 5, 2));         // 绕行同区
        Assert.Same(map.At(1, 5, 2), map.At(3, 5, 2));
    }

    [Fact]
    public void BuildLayout_NonStructuralSurface_DoesNotSeedRegions()
    {
        // 地板（structural）+ 一张非 structural 的桌子（有 Up 顶面 y=8）
        var l = new VoxelLayout<Entity>();
        l.BuildAt(SurfaceBox("floor", 5, 1, 5, new Int3(0, 0, 0), 1), new Int3(0, 0, 0));
        l.BuildAt(Box("ceiling", 5, 1, 5), new Int3(0, 30, 0));
        var table = Box("table", 3, 1, 3);
        var tableSurface = new GridLayout<bool>(new Int2(3, 3), new Int3(1, 8, 1));
        tableSurface.Fill(true, Int2.Zero, new Int2(3, 3));
        table.AddComponent(new PlacementLayoutSource { Layout = tableSurface });
        l.BuildAt(table, new Int3(1, 7, 1)); // 桌面 y=7 占用，顶面 y=8

        var map = Region.BuildLayout(l);

        Assert.Null(map.At(2, 8, 2));    // 桌顶不是行走面（非 structural）→ 不产站立格
        Assert.NotNull(map.At(2, 5, 2)); // 桌下 [1,7) 仍是区域
    }

    [Fact]
    public void BuildLayout_EmptyLayout_HasNoRegions()
    {
        var map = Region.BuildLayout(new VoxelLayout<Entity>());
        Assert.Null(map.At(0, 0, 0));
        Assert.Empty(map.AllSpans());
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
