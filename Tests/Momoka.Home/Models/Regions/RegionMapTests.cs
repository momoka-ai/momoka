using Xunit;
using Momoka.Home.Entities;
using Momoka.Home.Geometry;
using Momoka.Home.Layouts;
using Momoka.Home.Primitives;
using Momoka.Home.Regions;
namespace Momoka.Home.Tests.Models.Regions;

/// <summary>
/// RegionMap labels free-space column spans by region id via flood fill
/// (4-connectivity in XZ + interval overlap/step tolerance) and aggregates
/// per-region bounds / volume / footprint.
/// </summary>
public class RegionMapTests
{
    private static Entity Box(string path, int sx, int sy, int sz) => new()
    {
        Key = new Key(path),
        Volume = new Box3D { SizeX = sx, SizeY = sy, SizeZ = sz },
    };

    /// <summary>
    /// 10×9×30 封闭空间；中墙 (x=5, 全高) 把空间分为左右两室。墙体错位角接避免重叠。
    /// 左室 x=1..4、右室 x=6..8，z=1..8，自由 y=1..29。
    /// </summary>
    private static VoxelLayout<Entity> TwoRoomScene()
    {
        var l = new VoxelLayout<Entity>();
        l.BuildAt(Box("floor", 10, 1, 10), new Int3(0, 0, 0));    // y=0
        l.BuildAt(Box("ceiling", 10, 1, 10), new Int3(0, 30, 0)); // y=30
        l.BuildAt(Box("wall", 10, 29, 1), new Int3(0, 1, 0));     // 北 z=0
        l.BuildAt(Box("wall", 10, 29, 1), new Int3(0, 1, 9));     // 南 z=9
        l.BuildAt(Box("wall", 1, 29, 8), new Int3(0, 1, 1));      // 西 x=0, z=1..8
        l.BuildAt(Box("wall", 1, 29, 8), new Int3(9, 1, 1));      // 东 x=9, z=1..8
        l.BuildAt(Box("wall", 1, 29, 8), new Int3(5, 1, 1));      // 中 x=5, z=1..8
        return l;
    }

    [Fact]
    public void Build_SplitsByFullHeightWall()
    {
        var map = RegionMap.Build(TwoRoomScene());
        Assert.Equal(2, map.Regions.Count);

        var left = map.RegionAt(2, 5, 2);
        var right = map.RegionAt(7, 5, 2);
        Assert.NotNull(left);
        Assert.NotNull(right);
        Assert.NotEqual(left!.Id, right!.Id);

        Assert.Null(map.RegionAt(5, 5, 2));  // 中墙（阻隔）
        Assert.Null(map.RegionAt(2, 0, 2));  // 地板
        Assert.Null(map.RegionAt(2, 30, 2)); // 天花板
        Assert.Null(map.RegionAt(2, -1, 2)); // 空间外

        var leftRegion = map.Regions.Single(r => r.Area == 32);
        var rightRegion = map.Regions.Single(r => r.Area == 24);
        Assert.Equal(928, leftRegion.Volume); // 4×8×29
        Assert.Equal(696, rightRegion.Volume); // 3×8×29
        Assert.Equal(new Bound(new Int3(1, 1, 1), new Int3(4, 29, 8)), leftRegion.Bounds);
        Assert.Equal(new Bound(new Int3(6, 1, 1), new Int3(8, 29, 8)), rightRegion.Bounds);
    }

    [Fact]
    public void Build_MergesThroughDoorway()
    {
        var l = new VoxelLayout<Entity>();
        l.BuildAt(Box("floor", 10, 1, 10), new Int3(0, 0, 0));
        l.BuildAt(Box("ceiling", 10, 1, 10), new Int3(0, 30, 0));
        l.BuildAt(Box("wall", 10, 29, 1), new Int3(0, 1, 0));
        l.BuildAt(Box("wall", 10, 29, 1), new Int3(0, 1, 9));
        l.BuildAt(Box("wall", 1, 29, 8), new Int3(0, 1, 1));
        l.BuildAt(Box("wall", 1, 29, 8), new Int3(9, 1, 1));
        // 中墙留门洞 z=4：两段 [1..3] 与 [5..8]
        l.BuildAt(Box("wall", 1, 29, 3), new Int3(5, 1, 1));
        l.BuildAt(Box("wall", 1, 29, 4), new Int3(5, 1, 5));

        var map = RegionMap.Build(l);
        var room = Assert.Single(map.Regions);

        Assert.Same(room, map.RegionAt(2, 5, 2));
        Assert.Same(room, map.RegionAt(7, 5, 2)); // 门洞连通
        Assert.Equal(928 + 696 + 29, room.Volume); // 两室 + 门洞列
        Assert.Equal(32 + 24 + 1, room.Area);
    }

    [Fact]
    public void Build_StepToleranceConnectsNearbySpans()
    {
        // 相邻两列：A 自由 [1,2)，B 自由 [4,5)，间距 2（B 下方基座堵住）
        var l = new VoxelLayout<Entity>();
        l.BuildAt(Box("floor", 1, 1, 1), new Int3(0, 0, 0));   // A 地板 y=0
        l.BuildAt(Box("ceiling", 1, 1, 1), new Int3(0, 2, 0)); // A 天花 y=2
        l.BuildAt(Box("floor", 1, 3, 1), new Int3(1, 0, 0));   // B 基座 y=0..2
        l.BuildAt(Box("floor", 1, 1, 1), new Int3(1, 3, 0));   // B 地板 y=3
        l.BuildAt(Box("ceiling", 1, 1, 1), new Int3(1, 5, 0)); // B 天花 y=5

        Assert.Single(RegionMap.Build(l, new RegionRules { MaxStep = 2 }).Regions); // 步高内 → 连通
        Assert.Equal(2, RegionMap.Build(l, new RegionRules { MaxStep = 1 }).Regions.Count); // 超出 → 断开
    }

    [Fact]
    public void Build_TallNonStructuralBlocks_ShortIsTransparent()
    {
        // 3×3 封闭房间（x=1..3, z=1..3, 自由 y=1..29）
        VoxelLayout<Entity> Room()
        {
            var l = new VoxelLayout<Entity>();
            l.BuildAt(Box("floor", 5, 1, 5), new Int3(0, 0, 0));
            l.BuildAt(Box("ceiling", 5, 1, 5), new Int3(0, 30, 0));
            l.BuildAt(Box("wall", 5, 29, 1), new Int3(0, 1, 0)); // 北 z=0
            l.BuildAt(Box("wall", 5, 29, 1), new Int3(0, 1, 4)); // 南 z=4
            l.BuildAt(Box("wall", 1, 29, 3), new Int3(0, 1, 1)); // 西 x=0, z=1..3
            l.BuildAt(Box("wall", 1, 29, 3), new Int3(4, 1, 1)); // 东 x=4, z=1..3
            return l;
        }

        // 高书架（20 cell ≥ 18）：阻隔
        var tall = Room();
        tall.BuildAt(Box("shelf", 1, 20, 1), new Int3(2, 1, 2));
        var map = RegionMap.Build(tall);
        var room = Assert.Single(map.Regions);
        Assert.Null(map.RegionAt(2, 10, 2));                  // 书架内部（阻隔）
        Assert.Same(room, map.RegionAt(2, 25, 2));            // 书架上方仍连通
        Assert.Same(room, map.RegionAt(1, 5, 2));             // 绕过书架同区
        Assert.Same(map.RegionAt(1, 5, 2), map.RegionAt(3, 5, 2));

        // 矮书架（3 cell < 18）：透明，占据格视为自由
        var shortShelf = Room();
        shortShelf.BuildAt(Box("shelf", 1, 3, 1), new Int3(2, 1, 2));
        var map2 = RegionMap.Build(shortShelf);
        var room2 = Assert.Single(map2.Regions);
        Assert.Same(room2, map2.RegionAt(2, 5, 2));           // 透明 → 仍是房间
    }

    [Fact]
    public void Build_EmptyLayout_HasNoRegions()
    {
        var map = RegionMap.Build(new VoxelLayout<Entity>());
        Assert.Empty(map.Regions);
        Assert.Null(map.RegionAt(0, 0, 0));
    }
}
