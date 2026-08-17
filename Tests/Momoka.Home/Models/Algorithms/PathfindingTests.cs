using Xunit;
using Momoka.Home.Algorithms;
using Momoka.Home.Primitives;
namespace Momoka.Home.Tests.Models.Algorithms;

/// <summary>
/// Weighted A*（<see cref="Pathfinding.AStar"/>）：可达路径重建、不可达返回 null、
/// maxCost 剪枝、代价累计与 Position.Scale 携带。expand / heuristic 均为参数化委托，
/// 测试在 XZ 平面 4 连通网格上做纯代数验证。
/// </summary>
public class PathfindingTests
{
    private static IEnumerable<Int3> Neighbors4(Int3 cell)
    {
        yield return cell.Offset(1, 0, 0);
        yield return cell.Offset(-1, 0, 0);
        yield return cell.Offset(0, 0, 1);
        yield return cell.Offset(0, 0, -1);
    }

    private static Pathfinding.Result? Find2D(
        int sx, int sz, int gx, int gz,
        HashSet<Int3> blocked,
        double maxCost = double.PositiveInfinity)
    {
        return Pathfinding.AStar(
            new Position(new Int3(sx, 0, sz), 10f),
            n => n.X == gx && n.Z == gz,
            n => Neighbors4(n).Where(nb => !blocked.Contains(nb)).Select(nb => (nb, 1.0)),
            n => Math.Abs(n.X - gx) + Math.Abs(n.Z - gz),
            maxCost);
    }

    private static bool IsAdjacentStep(Int3 a, Int3 b) =>
        Math.Abs(a.X - b.X) + Math.Abs(a.Z - b.Z) == 1;

    [Fact]
    public void StraightLine_FindsShortestPath()
    {
        var result = Find2D(0, 0, 3, 3, new HashSet<Int3>());

        Assert.NotNull(result);
        Assert.Equal(6.0, result.Value.Distance); // 曼哈顿 3+3
        Assert.Equal(7, result.Value.Path.Count);  // 起点 + 6 步
        Assert.Equal(new Position(new Int3(0, 0, 0), 10f), result.Value.Path[0]);
        Assert.Equal(new Position(new Int3(3, 0, 3), 10f), result.Value.Path[^1]);

        // 每对相邻途经点都是 4-连通的邻格
        for (var i = 1; i < result.Value.Path.Count; i++)
            Assert.True(IsAdjacentStep(result.Value.Path[i - 1].AsInt3(), result.Value.Path[i].AsInt3()));
    }

    [Fact]
    public void Obstacle_GoesAround()
    {
        // 全高墙：x=2、z=0..6 全部阻塞 → 需绕行（借道 z=-1 侧，代价 +2）
        var blocked = new HashSet<Int3>();
        for (var z = 0; z <= 6; z++)
            blocked.Add(new Int3(2, 0, z));

        var result = Find2D(0, 0, 4, 4, blocked);

        Assert.NotNull(result);
        Assert.Equal(10.0, result.Value.Distance); // 8 + 2 绕行
        Assert.DoesNotContain(result.Value.Path, p => blocked.Contains(p.AsInt3()));
    }

    [Fact]
    public void Unreachable_EnclosedGoal_ReturnsNull()
    {
        // 目标 (5,0,5) 被 1 格厚环形墙完全包围
        var blocked = new HashSet<Int3>();
        for (var k = 4; k <= 6; k++)
        {
            blocked.Add(new Int3(4, 0, k));
            blocked.Add(new Int3(6, 0, k));
            blocked.Add(new Int3(k, 0, 4));
            blocked.Add(new Int3(k, 0, 6));
        }

        Assert.Null(Find2D(0, 0, 5, 5, blocked, maxCost: 50));
    }

    [Fact]
    public void StartIsGoal_ReturnsSingleWaypoint()
    {
        var result = Find2D(0, 0, 0, 0, new HashSet<Int3>());

        Assert.NotNull(result);
        Assert.Equal(0.0, result.Value.Distance);
        Assert.Single(result.Value.Path);
    }

    [Fact]
    public void MaxCost_PrunesPathsBeyondBudget()
    {
        // 直线距离 20 步，预算 5 → 剪枝后不可达
        Assert.Null(Find2D(0, 0, 10, 10, new HashSet<Int3>(), maxCost: 5));
    }

    [Fact]
    public void MaxCost_ExactBudget_StillReachable()
    {
        var result = Find2D(0, 0, 3, 3, new HashSet<Int3>(), maxCost: 6);

        Assert.NotNull(result);
        Assert.Equal(6.0, result.Value.Distance);
    }

    [Fact]
    public void PathPositions_CarryStartScale()
    {
        var result = Find2D(0, 0, 1, 0, new HashSet<Int3>());

        Assert.All(result!.Value.Path, p => Assert.Equal(10f, p.Scale));
        Assert.Equal(new Float3(10, 0, 0), result.Value.Path[^1].Absolute()); // 格 (1,0,0) → cm (10,0,0)
    }

    [Fact]
    public void WeightedExpand_AccumulatesCosts()
    {
        // 每步代价 2：0 → 1 → 2 = 4
        var result = Pathfinding.AStar(
            new Position(Int3.Zero, 10f),
            n => n.X == 2,
            n => new[] { (n.Offset(1, 0, 0), 2.0) },
            n => n.X, // 启发式 = 到目标的剩余步数 × 1（低估，可接受）
            double.PositiveInfinity);

        Assert.NotNull(result);
        Assert.Equal(4.0, result.Value.Distance);
        Assert.Equal(3, result.Value.Path.Count);
    }
}
