using Momoka.Home.Levels.Layouts;
using Momoka.Home.Primitives;
namespace Momoka.Home.Levels;

/// <summary>
/// Path-finding over the voxel space. The voxel cell (<see cref="Int3"/>) is
/// this project's one fundamental grid node — every object sits on the 10 cm
/// grid, and mobile entities (people, pets, robots) snap to it for movement.
/// The search therefore addresses cells (<see cref="Int3"/>, exact integer
/// keys) but reports coordinates as self-describing <see cref="Position"/>s
/// whose scale is taken from the start position — one result type, no
/// intermediate artifact. Move rules stay parametric via the
/// <c>expand</c> delegate, so walkability (agent size, climb
/// limits) remains with the caller (e.g. pathfinding over an
/// <see cref="IVoxelSource{T}"/>).
/// </summary>
public static class Pathfinding
{
    /// <summary>A path over a voxel grid: waypoints as self-describing
    /// <see cref="Position"/>s (scale = cell size; <c>Absolute()</c> = cm) and
    /// the total travelled distance. 失败（不可达 / 超出预算）由可空返回
    /// （<c>Result?</c> 的 null）表达，故结果本身恒为成功路径。</summary>
    public readonly record struct Result(IReadOnlyList<Position> Path, double Distance);

    /// <summary>
    /// Weighted A* over a voxel grid: returns the first cell satisfying
    /// <paramref name="isGoal"/> as a path of <see cref="Position"/>s in the
    /// grid's scale (cell × scale = cm), or null when unreachable within
    /// <paramref name="maxCost"/> (cells whose accumulated cost already
    /// exceeds it are pruned). <paramref name="start"/> must carry the grid's
    /// cell scale — e.g. <c>src.Rescale(voxels.Length)</c> or
    /// <c>new Position(cell, voxels.Length)</c> — and is snapped to a cell via
    /// <see cref="Position.AsInt3"/> for the search. The heuristic must be
    /// admissible — never overestimate; pass a zero heuristic to get Dijkstra.
    /// </summary>
    public static Result? AStar(
        Position start,
        Func<Int3, bool> isGoal,
        Func<Int3, IEnumerable<(Int3 Node, double Cost)>> expand,
        Func<Int3, double> heuristic,
        double maxCost = double.PositiveInfinity)
    {
        var startCell = start.AsInt3();
        var cameFrom = new Dictionary<Int3, Int3>();
        var gScore = new Dictionary<Int3, double> { [startCell] = 0 };
        var open = new PriorityQueue<Int3, double>();
        open.Enqueue(startCell, 0);

        Int3? goal = null;
        while (open.Count > 0)
        {
            var current = open.Dequeue();
            if (isGoal(current))
            {
                goal = current;
                break;
            }

            var g = gScore[current];
            foreach (var (next, cost) in expand(current))
            {
                var tentative = g + cost;
                if (tentative > maxCost)
                    continue;
                if (!gScore.TryGetValue(next, out var best) || tentative < best)
                {
                    gScore[next] = tentative;
                    cameFrom[next] = current;
                    open.Enqueue(next, tentative + heuristic(next));
                }
            }
        }

        if (goal is null)
            return null;

        var path = new List<Position>();
        var node = goal.Value;
        path.Add(new Position(node, start.Scale));
        while (cameFrom.TryGetValue(node, out var prev))
        {
            path.Add(new Position(prev, start.Scale));
            node = prev;
        }
        path.Reverse();
        return new Result(path, gScore[goal.Value]);
    }
}
