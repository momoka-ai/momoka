using System.Numerics;
using Xunit;
using Momoka.Home;
using Momoka.Home.Algorithms;
using Momoka.Home.Entities;
using Momoka.Home.Layouts;
using Momoka.Home.Geometry;
using Momoka.Home.Primitives;
using Momoka.Home.Properties;
namespace Momoka.Home.Tests.Models.Algorithms;

/// <summary>
/// <see cref="VoxelSourceExtensions"/> 的空间查询调用例：遮挡 / 视线（IsOccluded、
/// CanSee 三态）、视野内目标（FindItemsOnLine 射线 + FindItemsInCone 锥体、Occlusion 各档位）、
/// 碰撞（点 / 球 / 体积）与寻路（FindPath）。全部以 UnitLayout 为
/// <see cref="IVoxelSource{T}"/> 宿主，坐标世界 cm、内部对齐 10cm 体素格。
/// </summary>
public class VoxelSourceQueryTests
{
    private static Entity Box(string path) => new() { Key = path, Volume = new Box3D() };

    private static Entity StructuralBox(string path)
    {
        var entity = Box(path);
        entity.AddProperties(new[] { new BooleanProperty(Property.IsImmutable, true) });
        return entity;
    }

    private static Entity TransparentBox(string path)
    {
        var entity = Box(path);
        entity.AddProperties(new[] { new BooleanProperty(Property.IsTransparent, true) });
        return entity;
    }

    /// <summary>空网格，无 Bound（视线 / 命中 / 碰撞不需要）。</summary>
    private static UnitLayout EmptyUnit() => new();

    /// <summary>20×20×20 格 Bound + y=0 的结构地板（x,z ∈ [0,9]）——寻路场景。</summary>
    private static UnitLayout FlooredUnit()
    {
        var unit = new UnitLayout();
        unit.Voxels.Bound = Bound.FromCorners(Int3.Zero.ToFloat3(), new Int3(200, 200, 200).ToFloat3());
        var floor = StructuralBox("floor");
        for (var x = 0; x <= 9; x++)
            for (var z = 0; z <= 9; z++)
                unit.Voxels[new Int3(x, 0, z)] = floor;
        return unit;
    }

    private static void AssertNear(Float3 expected, Float3 actual, float tolerance = 1e-3f) =>
        Assert.True((expected - actual).Magnitude <= tolerance, $"expected {expected}, got {actual}");

    // ── 遮挡 / 视线 ──────────────────────────────────────

    [Fact]
    public void IsOccluded_ClearLine_ReturnsFalse()
    {
        var unit = EmptyUnit();

        Assert.False(unit.IsOccluded(new Position(new Float3(0, 0, 0)), new Position(new Float3(30, 0, 0))));
    }

    [Fact]
    public void IsOccluded_WallBetween_ReturnsTrue()
    {
        var unit = EmptyUnit();
        unit.Voxels[new Int3(1, 0, 0)] = StructuralBox("wall");

        Assert.True(unit.IsOccluded(new Position(new Float3(0, 0, 0)), new Position(new Float3(30, 0, 0))));
    }

    [Fact]
    public void IsOccluded_DestIsTheWallItself_ReturnsFalse()
    {
        var unit = EmptyUnit();
        unit.Voxels[new Int3(1, 0, 0)] = StructuralBox("wall");

        // 终点格是墙本身 → "看向一堵墙"不算被遮挡
        Assert.False(unit.IsOccluded(new Position(new Float3(0, 0, 0)), new Position(new Float3(10, 0, 0))));
    }

    [Fact]
    public void IsOccluded_StartCellWall_IsIgnored()
    {
        var unit = EmptyUnit();
        unit.Voxels[new Int3(0, 0, 0)] = StructuralBox("wall");

        Assert.False(unit.IsOccluded(new Position(new Float3(0, 0, 0)), new Position(new Float3(30, 0, 0))));
    }

    [Fact]
    public void CanSee_True_WhenClear_False_WhenBlocked()
    {
        var clear = EmptyUnit();
        Assert.True(clear.CanSee(new Position(new Float3(0, 0, 0)), new Position(new Float3(30, 0, 0))));

        var blocked = EmptyUnit();
        blocked.Voxels[new Int3(1, 0, 0)] = StructuralBox("wall");
        Assert.False(blocked.CanSee(new Position(new Float3(0, 0, 0)), new Position(new Float3(30, 0, 0))));
    }

    [Fact]
    public void CanSee_Bound_PicksNearestPointOnBox()
    {
        var clear = EmptyUnit();
        Assert.True(clear.CanSee(new Position(new Float3(0, 0, 0)),
            new Bound(new Float3(50, 0, 50), new Float3(150, 120, 150))));

        var blocked = EmptyUnit();
        blocked.Voxels[new Int3(2, 0, 2)] = StructuralBox("wall"); // 视线 (0,0,0)→(50,0,50) 必经
        Assert.False(blocked.CanSee(new Position(new Float3(0, 0, 0)),
            new Bound(new Float3(50, 0, 50), new Float3(150, 120, 150))));
    }

    [Fact]
    public void CanSee_Directional_AppliesConeConstraint()
    {
        var unit = EmptyUnit();

        Assert.True(unit.CanSee(new Position(new Float3(0, 0, 0)), new Position(new Float3(80, 0, 0)),
            Vector3.UnitX, maxDistance: 100, maxRadius: 20));
        Assert.False(unit.CanSee(new Position(new Float3(0, 0, 0)), new Position(new Float3(50, 0, 50)),
            Vector3.UnitX, maxDistance: 100, maxRadius: 20));  // 垂距 50 > 20
        Assert.False(unit.CanSee(new Position(new Float3(0, 0, 0)), new Position(new Float3(150, 0, 0)),
            Vector3.UnitX, maxDistance: 100, maxRadius: 20)); // 超射程
        Assert.False(unit.CanSee(new Position(new Float3(0, 0, 0)), new Position(new Float3(50, 0, 0)),
            Vector3.Zero, maxDistance: 100, maxRadius: 20));  // 零方向
    }

    // ── 视野内目标：射线 ─────────────────────────────────

    [Fact]
    public void FindItemsOnLine_Ray_ReturnsHitsNearToFar()
    {
        var unit = EmptyUnit();
        var wall = StructuralBox("wall");
        var chair = Box("chair");
        unit.Voxels[new Int3(1, 0, 0)] = wall;
        unit.Voxels[new Int3(2, 0, 0)] = chair;

        var hits = unit.FindItemsOnLine(new Position(new Float3(0, 0, 0)), Vector3.UnitX, 100).ToList();

        Assert.Equal(2, hits.Count);
        Assert.Same(wall, hits[0].Hit);  // 近
        Assert.Same(chair, hits[1].Hit); // 远
        Assert.Equal(new Int3(1, 0, 0), hits[0].Cell);
        AssertNear(new Float3(5, 0, 0), hits[0].Point.Pos); // 格边界进入点
    }

    [Fact]
    public void FindItemsOnLine_Ray_OnlyImmutable_StopsAtWall()
    {
        var unit = EmptyUnit();
        unit.Voxels[new Int3(1, 0, 0)] = StructuralBox("wall");
        unit.Voxels[new Int3(2, 0, 0)] = Box("chair");

        var hits = unit.FindItemsOnLine(new Position(new Float3(0, 0, 0)), Vector3.UnitX, 100,
            Occlusion.OnlyImmutable).ToList();

        Assert.Single(hits);
        Assert.Equal("wall", hits[0].Hit.Key.Path);
    }

    [Fact]
    public void FindItemsOnLine_Ray_Everything_ReturnsNearestOnly()
    {
        var unit = EmptyUnit();
        unit.Voxels[new Int3(1, 0, 0)] = Box("chair");
        unit.Voxels[new Int3(2, 0, 0)] = StructuralBox("wall");

        var hits = unit.FindItemsOnLine(new Position(new Float3(0, 0, 0)), Vector3.UnitX, 100,
            Occlusion.Everything).ToList();

        Assert.Single(hits);
        Assert.Equal("chair", hits[0].Hit.Key.Path);
    }

    [Fact]
    public void FindItemsOnLine_Ray_OnlyNonTransparent_PassesTransparentThenStops()
    {
        var unit = EmptyUnit();
        unit.Voxels[new Int3(1, 0, 0)] = TransparentBox("glass");
        unit.Voxels[new Int3(2, 0, 0)] = StructuralBox("wall");

        var hits = unit.FindItemsOnLine(new Position(new Float3(0, 0, 0)), Vector3.UnitX, 100,
            Occlusion.OnlyNonTransparent).ToList();

        Assert.Equal(2, hits.Count); // 玻璃穿透、墙阻挡
        Assert.Equal("glass", hits[0].Hit.Key.Path);
        Assert.Equal("wall", hits[1].Hit.Key.Path);
    }

    [Fact]
    public void FindItemsOnLine_Ray_DedupesMultiCellEntities()
    {
        var unit = EmptyUnit();
        var sofa = Box("sofa");
        unit.Voxels[new Int3(1, 0, 0)] = sofa;
        unit.Voxels[new Int3(2, 0, 0)] = sofa;

        var hits = unit.FindItemsOnLine(new Position(new Float3(0, 0, 0)), Vector3.UnitX, 100).ToList();

        Assert.Single(hits);
        Assert.Same(sofa, hits[0].Hit);
    }

    [Fact]
    public void FindItemsOnLine_Ray_ZeroDirection_ReturnsEmpty()
    {
        Assert.Empty(EmptyUnit().FindItemsOnLine(new Position(new Float3(0, 0, 0)), Vector3.Zero, 100));
    }

    // ── 视野内目标：锥体 ─────────────────────────────────

    [Fact]
    public void FindItemsInCone_KeepsInCone_AndSortsNearToFar()
    {
        var unit = EmptyUnit();
        unit.Voxels[new Int3(6, 5, 5)] = Box("near");     // 轴上，t=10
        unit.Voxels[new Int3(7, 5, 5)] = Box("far");      // 轴上，t=20
        unit.Voxels[new Int3(7, 5, 8)] = Box("sideways"); // 垂距 30 > 锥内阈值 6

        var hits = unit.FindItemsInCone(new Position(new Float3(50, 50, 50)), Vector3.UnitX, 100, coneRadiusAtDistance: 30).ToList();

        Assert.Equal(2, hits.Count);
        Assert.Equal("near", hits[0].Hit.Key.Path);
        Assert.Equal("far", hits[1].Hit.Key.Path);
    }

    [Fact]
    public void FindItemsInCone_OnlyImmutable_SkipsOccludedEntities()
    {
        var unit = EmptyUnit();
        unit.Voxels[new Int3(6, 5, 5)] = StructuralBox("wall");
        unit.Voxels[new Int3(7, 5, 5)] = Box("behind");

        var hits = unit.FindItemsInCone(new Position(new Float3(50, 50, 50)), Vector3.UnitX, 100,
            coneRadiusAtDistance: 30, occlusion: Occlusion.OnlyImmutable).ToList();

        Assert.Single(hits); // "behind" 被墙遮挡 → 跳过
        Assert.Equal("wall", hits[0].Hit.Key.Path);
    }

    // ── 碰撞 ─────────────────────────────────────────────

    [Fact]
    public void IsCollided_Point_HitsOccupiedCell()
    {
        IVoxelSource<Entity> unit = EmptyUnit();
        var wall = StructuralBox("wall");
        unit.Voxels[new Int3(2, 0, 2)] = wall;

        var hit = unit.IsCollided(new Position(new Float3(20, 0, 20)));

        Assert.NotNull(hit);
        Assert.Same(wall, hit!.Value.Hit);
        Assert.Equal(new Int3(2, 0, 2), hit.Value.Cell);
        Assert.Equal(new Position(new Float3(20, 0, 20)), hit.Value.Point);
    }

    [Fact]
    public void IsCollided_Point_EmptyCell_ReturnsNull()
    {
        IVoxelSource<Entity> unit = EmptyUnit();
        Assert.Null(unit.IsCollided(new Position(new Float3(50, 0, 50))));
    }

    [Fact]
    public void IsCollided_Sphere_FindsEntityWithinRadius()
    {
        IVoxelSource<Entity> unit = EmptyUnit();
        var box = Box("box");
        unit.Voxels[new Int3(6, 5, 5)] = box;

        var hit = unit.IsCollided(new Position(new Float3(50, 50, 50)), 30f);

        Assert.NotNull(hit);
        Assert.Same(box, hit!.Value.Hit);
    }

    [Fact]
    public void IsCollided_Sphere_OutsideRadius_ReturnsNull()
    {
        IVoxelSource<Entity> unit = EmptyUnit();
        unit.Voxels[new Int3(10, 10, 10)] = Box("box");

        Assert.Null(unit.IsCollided(new Position(new Float3(50, 50, 50)), 30f));
    }

    [Fact]
    public void IsCollidedVolume_OverlappingVolume_ReturnsHit()
    {
        IVoxelSource<Entity> unit = EmptyUnit();
        var existing = Box("existing");
        unit.Voxels[new Int3(2, 0, 2)] = existing;
        var volume = new Box3D { SizeX = 2, SizeY = 1, SizeZ = 2 };

        var hit = unit.IsCollidedVolume(new Position(new Float3(20, 0, 20)), volume);

        Assert.NotNull(hit);
        Assert.Same(existing, hit!.Value.Hit);
        Assert.Equal(new Int3(2, 0, 2), hit.Value.Cell);
    }

    [Fact]
    public void IsCollidedVolume_ClearArea_ReturnsNull()
    {
        IVoxelSource<Entity> unit = EmptyUnit();
        var volume = new Box3D { SizeX = 2, SizeY = 1, SizeZ = 2 };

        Assert.Null(unit.IsCollidedVolume(new Position(new Float3(50, 0, 50)), volume));
    }

    // ── 寻路 ─────────────────────────────────────────────

    private static readonly Agent Human = Agent.Human;

    [Fact]
    public void FindPath_OpenFloor_ReachesGoal()
    {
        var unit = FlooredUnit();

        var result = unit.FindPath(
            new Position(new Int3(1, 1, 1), 10f),
            new Position(new Int3(8, 1, 8), 10f),
            Human, maxDistance: 2000);

        Assert.NotNull(result);
        Assert.Equal(new Int3(1, 1, 1), result.Value.Path[0].AsInt3());
        Assert.Equal(new Int3(8, 1, 8), result.Value.Path[^1].AsInt3());
        Assert.Equal(14.0, result.Value.Distance); // 曼哈顿 7+7，无爬升
    }

    [Fact]
    public void FindPath_NoBound_ReturnsNull()
    {
        Assert.Null(new UnitLayout().FindPath(
            new Position(new Float3(0, 0, 0)), new Position(new Float3(50, 0, 50)), Human));
    }

    [Fact]
    public void FindPath_FullHeightWall_SeparatesSides()
    {
        var unit = FlooredUnit();
        var wall = StructuralBox("wall");
        // 墙铺满整列（x=5, z=0..20, y=0..20），无缺口、Bound 内无绕行通道
        for (var z = 0; z <= 20; z++)
            for (var y = 0; y <= 20; y++)
                unit.Voxels[new Int3(5, y, z)] = wall;

        var result = unit.FindPath(
            new Position(new Int3(1, 1, 1), 10f),
            new Position(new Int3(8, 1, 8), 10f),
            Human, maxDistance: 2000);

        Assert.Null(result);
    }

    [Fact]
    public void FindPath_GoesThroughGapInWall()
    {
        var unit = FlooredUnit();
        var wall = StructuralBox("wall");
        // 中墙 x=5 留缺口 z=5：z=0..4 与 z=6..20 两段，y 全高
        for (var y = 0; y <= 20; y++)
        {
            for (var z = 0; z <= 4; z++)
                unit.Voxels[new Int3(5, y, z)] = wall;
            for (var z = 6; z <= 20; z++)
                unit.Voxels[new Int3(5, y, z)] = wall;
        }

        var result = unit.FindPath(
            new Position(new Int3(1, 1, 1), 10f),
            new Position(new Int3(8, 1, 8), 10f),
            Human, maxDistance: 2000);

        Assert.NotNull(result);
        Assert.Contains(result.Value.Path, p => p.AsInt3() == new Int3(5, 1, 5)); // 借道门洞
        Assert.Equal(14.0, result.Value.Distance);
    }

    [Fact]
    public void FindPath_ClimbWithinLimit_CrossesStep()
    {
        var unit = new UnitLayout();
        unit.Voxels.Bound = Bound.FromCorners(Int3.Zero.ToFloat3(), new Int3(200, 200, 200).ToFloat3());
        var floor = StructuralBox("floor");
        var platform = StructuralBox("platform");
        for (var x = 0; x <= 4; x++)   // 左地平面 y=0（站立 y=1）
            for (var z = 0; z <= 9; z++)
                unit.Voxels[new Int3(x, 0, z)] = floor;
        for (var x = 5; x <= 9; x++)   // 右平台面 y=2（站立 y=3，高差 2 ≤ 人类爬升 2）
            for (var z = 0; z <= 9; z++)
                unit.Voxels[new Int3(x, 2, z)] = platform;

        var result = unit.FindPath(
            new Position(new Int3(1, 1, 1), 10f),
            new Position(new Int3(7, 3, 7), 10f),
            Human, maxDistance: 2000);

        Assert.NotNull(result);
        Assert.Contains(result.Value.Path, p => p.AsInt3() == new Int3(5, 3, 5)); // 跨过台阶
        Assert.Equal(12.2, result.Value.Distance); // XZ 曼哈顿 12 步 + 2 格爬升 × 0.1
    }

    [Fact]
    public void FindPath_ClimbBeyondLimit_Unreachable()
    {
        var unit = new UnitLayout();
        unit.Voxels.Bound = Bound.FromCorners(Int3.Zero.ToFloat3(), new Int3(200, 200, 200).ToFloat3());
        var floor = StructuralBox("floor");
        var platform = StructuralBox("platform");
        for (var x = 0; x <= 4; x++)
            for (var z = 0; z <= 9; z++)
                unit.Voxels[new Int3(x, 0, z)] = floor;
        for (var x = 5; x <= 9; x++)
            for (var z = 0; z <= 9; z++)
                unit.Voxels[new Int3(x, 2, z)] = platform;

        var robot = new Agent(Height: 120, Radius: 30, MaxClimbHeight: 0, MaxJumpHeight: 0,
            MaxWalkLength: 2000, MaxInteractLength: 60);

        var result = unit.FindPath(
            new Position(new Int3(1, 1, 1), 10f),
            new Position(new Int3(7, 3, 7), 10f),
            robot, maxDistance: 2000);

        Assert.Null(result);
    }
}
