using System.Numerics;
using Momoka.Home.Algorithms;
using Momoka.Home.Geometry;
using Momoka.Home.Primitives;
using Momoka.Home.Properties;

namespace Momoka.Home.Layouts;

public interface IVoxelSource<T> where T : notnull
{
    VoxelLayout<T> Voxels { get; }
}

public static class VoxelSourceExtensions
{
    public static bool CanSee<T>(this IVoxelSource<T> voxelSource, Position src, Position dest)
        where T : IPropertySource
    {
        var voxels = voxelSource.Voxels;
        var end = voxels.GetAsRelative(dest.Absolute());
        return !RayCells(voxels, src.Absolute(), dest.Absolute())
            .Skip(1)
            .TakeWhile(c => c.Cell != end)
            .Any(c => voxels[c.Cell].IsImmutable());
    }

    public static bool CanSee<T>(this IVoxelSource<T> voxelSource, Position src, Bound dest)
        where T : IPropertySource
    {
        var s = src.Absolute();
        var target = new Float3(
            Math.Clamp(s.X, dest.Min.X, dest.Max.X),
            Math.Clamp(s.Y, dest.Min.Y, dest.Max.Y),
            Math.Clamp(s.Z, dest.Min.Z, dest.Max.Z));
        return voxelSource.CanSee(src, new Position(target));
    }

    public static bool CanSee<T>(
        this IVoxelSource<T> voxelSource,
        Position src,
        Position dest,
        Vector3 direction,
        double maxDistance,
        double maxRadius) where T : IPropertySource
    {
        var dir = new Float3(direction.X, direction.Y, direction.Z);
        var toDest = dest.Absolute() - src.Absolute();
        var projected = toDest.X * dir.X + toDest.Y * dir.Y + toDest.Z * dir.Z;
        if (projected < 0 || projected > maxDistance)
            return false;
        var lateral = (toDest - dir * (float)projected).Magnitude;
        if (lateral > maxRadius)
            return false;
        return voxelSource.CanSee(src, dest);
    }

    public static RayTracing.Result<T>? GetRayTracing<T>(
        this IVoxelSource<T> voxelSource,
        Position src,
        Vector3 d,
        double maxDistance,
        double maxRadius,
        Predicate<T>? filter = null) where T : notnull
    {
        var voxels = voxelSource.Voxels;
        var s = src.Absolute();
        var dirVec = new Float3(d.X, d.Y, d.Z);
        var magnitude = dirVec.Magnitude;
        if (magnitude < 1e-6f)
            return null;
        var dir = dirVec / magnitude;
        var to = s + dir * (float)maxDistance;
        var start = voxels.GetAsRelative(s);

        RayTracing.Result<T>? hit = null;
        var hitT = double.MaxValue;
        var seen = new HashSet<Int3>();

        foreach (var (cell, entry) in RayCells(voxels, s, to))
        {
            if (cell == start || !seen.Add(cell))
                continue;
            var value = voxels[cell];
            if (value is not null && (filter?.Invoke(value) ?? true))
            {
                var t = (entry - s).Magnitude;
                if (t < hitT)
                {
                    hitT = t;
                    hit = new RayTracing.Result<T>(value, cell, new Position(entry), t);
                }
            }

            if (maxRadius <= 0)
                continue;
            foreach (var c in Int3.Range(cell.Offset(-1, -1, -1), cell.Offset(1, 1, 1)))
            {
                if (c == start || !seen.Add(c))
                    continue;
                var v = voxels[c];
                if (v is null || !(filter?.Invoke(v) ?? true))
                    continue;
                var center = voxels.GetAsAbsolute(c) + new Float3(voxels.Length / 2f);
                var t = (center - s).X * dir.X + (center - s).Y * dir.Y + (center - s).Z * dir.Z;
                if (t < 0 || t > maxDistance)
                    continue;
                var lateral = (center - (s + dir * (float)t)).Magnitude;
                if (lateral > maxRadius)
                    continue;
                if (t < hitT)
                {
                    hitT = t;
                    hit = new RayTracing.Result<T>(v, c, new Position(s + dir * (float)t), t);
                }
            }
        }
        return hit;
    }

    public static Collision.Result<T>? IsCollided<T>(
        this IVoxelSource<T> voxelSource,
        Position src) where T : notnull
    {
        var voxels = voxelSource.Voxels;
        var cell = voxels.GetAsRelative(src.Absolute());
        var value = voxels[cell];
        return value is null
            ? null
            : new Collision.Result<T>(true, value, cell, new Position(voxels.GetAsAbsolute(cell)));
    }

    public static Collision.Result<T>? IsCollided<T>(
        this IVoxelSource<T> voxelSource,
        Position src,
        float radius) where T : notnull
    {
        var voxels = voxelSource.Voxels;
        var s = src.Absolute();
        var center = voxels.GetAsRelative(s);
        var r = (int)Math.Ceiling(radius / voxels.Length);
        var radiusSq = (double)radius * radius;
        foreach (var cell in Int3.Range(center.Offset(-r, -r, -r), center.Offset(r, r, r)))
        {
            var value = voxels[cell];
            if (value is null)
                continue;
            var p = voxels.GetAsAbsolute(cell) + new Float3(voxels.Length / 2f) - s;
            if (p.X * p.X + p.Y * p.Y + p.Z * p.Z <= radiusSq)
                return new Collision.Result<T>(true, value, cell, new Position(voxels.GetAsAbsolute(cell)));
        }
        return null;
    }

    public static Collision.Result<T>? IsCollidedVolume<T>(
        this IVoxelSource<T> voxelSource,
        Position src,
        Volume volume) where T : notnull
    {
        var voxels = voxelSource.Voxels;
        var anchor = voxels.GetAsRelative(src.Absolute());
        var hit = volume.Cells3D()
            .Select(offset => anchor + offset)
            .Select(cell => (Cell: cell, Value: voxels[cell]))
            .FirstOrDefault(x => x.Value is not null);
        return hit.Value is null
            ? null
            : new Collision.Result<T>(true, hit.Value, hit.Cell, new Position(voxels.GetAsAbsolute(hit.Cell)));
    }

    public static Pathfinding.Result? TryPathfinding<T>(
        this IVoxelSource<T> voxelSource,
        Position src,
        Position dest,
        Agent agent) where T : IPropertySource =>
        voxelSource.TryPathfinding(src, dest, agent, agent.MaxWalkLength);

    public static Pathfinding.Result? TryPathfinding<T>(
        this IVoxelSource<T> voxelSource,
        Position src,
        Position dest,
        Agent agent,
        float maxDistance) where T : IPropertySource
    {
        var voxels = voxelSource.Voxels;
        if (!voxels.Bound.Valid)
            return null;

        var length = voxels.Length;
        var height = Math.Max(1, (int)Math.Ceiling(agent.Height / length));
        var maxClimb = (int)Math.Ceiling(agent.MaxClimbHeight / length);
        var budget = maxDistance / length;

        var start = voxels.GetAsRelative(src.Absolute());
        var goal = voxels.GetAsRelative(dest.Absolute());
        var min = voxels.GetAsRelative(voxels.Bound.Min);
        var max = voxels.GetAsRelative(voxels.Bound.Max);

        bool IsImmutableAt(int x, int y, int z) =>
            voxels[new Int3(x, y, z)].IsImmutable();

        bool CanStand(int x, int y, int z)
        {
            if (x < min.X || x > max.X || y < min.Y || y > max.Y || z < min.Z || z > max.Z)
                return false;
            if (IsImmutableAt(x, y, z))
                return false;
            for (var k = 1; k < height; k++)
                if (IsImmutableAt(x, y + k, z))
                    return false;
            return y == min.Y || IsImmutableAt(x, y - 1, z);
        }

        int? StandYAt(int x, int z, int fromY)
        {
            for (var y = fromY + maxClimb; y >= min.Y; y--)
                if (CanStand(x, y, z))
                    return y;
            return null;
        }

        var startY = StandYAt(start.X, start.Z, start.Y) ?? start.Y;
        var goalY = StandYAt(goal.X, goal.Z, goal.Y) ?? goal.Y;

        var startNode = (X: start.X, Y: startY, Z: start.Z);

        var cameFrom = new Dictionary<(int X, int Y, int Z), (int X, int Y, int Z)>();
        var gScore = new Dictionary<(int X, int Y, int Z), double> { [startNode] = 0 };
        var open = new PriorityQueue<(int X, int Y, int Z), double>();
        open.Enqueue(startNode, 0);

        var directions = new[] { (1, 0), (-1, 0), (0, 1), (0, -1) };
        (int X, int Y, int Z)? found = null;

        while (open.Count > 0)
        {
            var current = open.Dequeue();
            if (current.X == goal.X && current.Z == goal.Z && Math.Abs(current.Y - goalY) <= maxClimb)
            {
                found = current;
                break;
            }
            var g = gScore[current];
            foreach (var (dx, dz) in directions)
            {
                var ny = StandYAt(current.X + dx, current.Z + dz, current.Y);
                if (ny is null)
                    continue;
                var next = (X: current.X + dx, Y: ny.Value, Z: current.Z + dz);
                var tentative = g + 1 + Math.Max(0, ny.Value - current.Y) * 0.1;
                if (tentative > budget)
                    continue;
                if (!gScore.TryGetValue(next, out var best) || tentative < best)
                {
                    gScore[next] = tentative;
                    cameFrom[next] = current;
                    var h = Math.Abs(next.X - goal.X) + Math.Abs(next.Z - goal.Z) + Math.Abs(next.Y - goalY);
                    open.Enqueue(next, tentative + h);
                }
            }
        }

        if (found is null)
            return new Pathfinding.Result(false, Array.Empty<Position>(), 0);

        var path = new List<Position>();
        (int X, int Y, int Z)? node = found;
        while (node is not null)
        {
            path.Add(new Position(voxels.GetAsAbsolute(new Int3(node.Value.X, node.Value.Y, node.Value.Z))));
            node = cameFrom.TryGetValue(node.Value, out var prev) ? prev : null;
        }
        path.Reverse();
        return new Pathfinding.Result(true, path, gScore[found.Value]);
    }

    // ── ray traversal (Amanatides & Woo, round-centered cells) ────────────

    private static IEnumerable<(Int3 Cell, Float3 Entry)> RayCells<T>(VoxelLayout<T> voxels, Float3 from, Float3 to)
        where T : notnull
    {
        var length = voxels.Length;
        var cell = voxels.GetAsRelative(from);
        var end = voxels.GetAsRelative(to);
        var dir = to - from;

        var stepX = dir.X == 0 ? 0 : Math.Sign(dir.X);
        var stepY = dir.Y == 0 ? 0 : Math.Sign(dir.Y);
        var stepZ = dir.Z == 0 ? 0 : Math.Sign(dir.Z);

        var tMaxX = stepX == 0 ? double.PositiveInfinity : ((cell.X + stepX * 0.5) * length - from.X) / dir.X;
        var tMaxY = stepY == 0 ? double.PositiveInfinity : ((cell.Y + stepY * 0.5) * length - from.Y) / dir.Y;
        var tMaxZ = stepZ == 0 ? double.PositiveInfinity : ((cell.Z + stepZ * 0.5) * length - from.Z) / dir.Z;

        var tDeltaX = stepX == 0 ? double.PositiveInfinity : Math.Abs(length / dir.X);
        var tDeltaY = stepY == 0 ? double.PositiveInfinity : Math.Abs(length / dir.Y);
        var tDeltaZ = stepZ == 0 ? double.PositiveInfinity : Math.Abs(length / dir.Z);

        var entry = from;
        while (true)
        {
            yield return (cell, entry);
            if (cell == end)
                yield break;
            if (tMaxX < tMaxY && tMaxX < tMaxZ)
            {
                cell = new Int3(cell.X + stepX, cell.Y, cell.Z);
                entry = from + dir * (float)tMaxX;
                tMaxX += tDeltaX;
            }
            else if (tMaxY < tMaxZ)
            {
                cell = new Int3(cell.X, cell.Y + stepY, cell.Z);
                entry = from + dir * (float)tMaxY;
                tMaxY += tDeltaY;
            }
            else
            {
                cell = new Int3(cell.X, cell.Y, cell.Z + stepZ);
                entry = from + dir * (float)tMaxZ;
                tMaxZ += tDeltaZ;
            }
        }
    }
}