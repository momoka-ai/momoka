using Xunit;
using Momoka.Home.Algorithms;
using Momoka.Home.Layouts;
using Momoka.Home.Primitives;
namespace Momoka.Home.Tests.Models.Algorithms;

/// <summary>
/// 形状格遍历（<see cref="Traverse"/>）：直线 DDA（<see cref="Traverse.OnLine"/>）的
/// 穿越顺序与进入点、锥体（<see cref="Traverse.InCone"/>）与视锥
/// （<see cref="Traverse.InFrustum"/>）的包围盒 + 投影筛选与由近及远排序。
/// snap 复用 <see cref="VoxelLayout{T}"/> 的世界→格取整。
/// </summary>
public class TraverseTests
{
    private static readonly VoxelLayout<int> Grid = new(); // Length = 10

    private static Int3 Snap(Float3 world) => Grid.GetAsRelative(world);

    private static void AssertNear(Float3 expected, Float3 actual, float tolerance = 1e-3f) =>
        Assert.True((expected - actual).Magnitude <= tolerance, $"expected {expected}, got {actual}");

    private static void AssertOrderedByDistance(IEnumerable<(Int3 Cell, Float3 Point)> cells, Float3 origin)
    {
        var distances = cells.Select(c => (c.Point - origin).Magnitude).ToList();
        Assert.Equal(distances, distances.OrderBy(d => d).ToList());
    }

    // ── OnLine：直线 DDA 遍历 ────────────────────────────

    [Fact]
    public void OnLine_AxisAligned_VisitsEveryCellInOrder()
    {
        var cells = Traverse.OnLine(Float3.Zero, new Float3(30, 0, 0), 10f, Snap).ToList();

        Assert.Equal(
            new[] { new Int3(0, 0, 0), new Int3(1, 0, 0), new Int3(2, 0, 0), new Int3(3, 0, 0) },
            cells.Select(c => c.Cell));

        // 进入点：起点格为 from，之后为格边界交点（格中心取整 → 半格偏移处）
        AssertNear(Float3.Zero, cells[0].Entry);
        AssertNear(new Float3(5, 0, 0), cells[1].Entry);
        AssertNear(new Float3(15, 0, 0), cells[2].Entry);
        AssertNear(new Float3(25, 0, 0), cells[3].Entry);
    }

    [Fact]
    public void OnLine_Diagonal_CoversEveryCrossedCell()
    {
        // 与对角直线相交的走廊格：DDA 按 t 最小的轴推进
        var cells = Traverse.OnLine(Float3.Zero, new Float3(30, 0, 30), 10f, Snap).ToList();

        Assert.Equal(
            new[]
            {
                new Int3(0, 0, 0), new Int3(0, 0, 1), new Int3(1, 0, 1),
                new Int3(1, 0, 2), new Int3(2, 0, 2), new Int3(2, 0, 3), new Int3(3, 0, 3),
            },
            cells.Select(c => c.Cell));
    }

    [Fact]
    public void OnLine_NegativeDirection_DescendsThroughCells()
    {
        var cells = Traverse.OnLine(new Float3(30, 0, 0), Float3.Zero, 10f, Snap).ToList();

        Assert.Equal(
            new[] { new Int3(3, 0, 0), new Int3(2, 0, 0), new Int3(1, 0, 0), new Int3(0, 0, 0) },
            cells.Select(c => c.Cell));
    }

    [Fact]
    public void OnLine_SameCellSpan_YieldsOnce()
    {
        // from 与 to 都取整到格 (0,0,0)
        var cells = Traverse.OnLine(Float3.Zero, new Float3(4, 0, 0), 10f, Snap).ToList();

        Assert.Single(cells);
        Assert.Equal(Int3.Zero, cells[0].Cell);
    }

    // ── InCone：锥体遍历 ─────────────────────────────────

    [Fact]
    public void InCone_CoversNearAxisCells_OrderedByDistance()
    {
        var cells = Traverse.InCone(Float3.Zero, new Float3(10, 0, 0), 40f, 20f, 10f, Snap).ToList();

        Assert.NotEmpty(cells);
        Assert.Contains(cells, c => c.Cell == new Int3(1, 0, 0));
        Assert.Contains(cells, c => c.Cell == new Int3(2, 0, 0));
        Assert.Contains(cells, c => c.Cell == new Int3(4, 0, 0));
        Assert.DoesNotContain(cells, c => c.Cell == Int3.Zero);            // 起点格排除
        Assert.DoesNotContain(cells, c => c.Cell == new Int3(5, 0, 0));    // 超出射程
        Assert.DoesNotContain(cells, c => c.Cell == new Int3(2, 2, 0));    // 垂距超锥半径
        AssertOrderedByDistance(cells, Float3.Zero);
    }

    [Fact]
    public void InCone_ZeroRadius_OnlyAxisCells()
    {
        var cells = Traverse.InCone(Float3.Zero, new Float3(10, 0, 0), 40f, 0f, 10f, Snap).ToList();

        Assert.Equal(
            new[]
            {
                new Int3(1, 0, 0), new Int3(2, 0, 0), new Int3(3, 0, 0), new Int3(4, 0, 0),
            },
            cells.Select(c => c.Cell));
    }

    [Fact]
    public void InCone_ZeroDirection_ReturnsEmpty()
    {
        Assert.Empty(Traverse.InCone(Float3.Zero, Float3.Zero, 40f, 20f, 10f, Snap));
    }

    // ── InFrustum：视锥遍历 ──────────────────────────────

    [Fact]
    public void InFrustum_WidthAndHeightConstraints_SelectCells()
    {
        // dir=+X、up=+Y → right=+Z：宽沿 Z、高沿 Y，随距离线性扩大
        var cells = Traverse.InFrustum(
            Float3.Zero, new Float3(10, 0, 0), Float3.Up,
            maxDistance: 40f, halfWidthAtDistance: 5f, halfHeightAtDistance: 20f,
            length: 10f, snap: Snap).ToList();

        Assert.NotEmpty(cells);
        Assert.Contains(cells, c => c.Cell == new Int3(1, 0, 0));
        Assert.Contains(cells, c => c.Cell == new Int3(2, 0, 0));
        Assert.Contains(cells, c => c.Cell == new Int3(2, 1, 0));  // 高度边界（恰好在锥内）
        Assert.DoesNotContain(cells, c => c.Cell == new Int3(2, 0, 2)); // 超半宽
        Assert.DoesNotContain(cells, c => c.Cell == new Int3(2, 2, 0)); // 超半高
        AssertOrderedByDistance(cells, Float3.Zero);
    }

    [Fact]
    public void InFrustum_ZeroDirection_ReturnsEmpty()
    {
        Assert.Empty(Traverse.InFrustum(Float3.Zero, Float3.Zero, Float3.Up, 40f, 5f, 20f, 10f, Snap));
    }

    [Fact]
    public void InFrustum_UpParallelToDirection_ReturnsEmpty()
    {
        Assert.Empty(Traverse.InFrustum(Float3.Zero, Float3.Up, Float3.Up, 40f, 5f, 20f, 10f, Snap));
    }
}
