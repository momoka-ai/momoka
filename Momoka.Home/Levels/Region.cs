using Momoka.Home.Levels.Entities;
using Momoka.Home.Levels.Layouts;
using Momoka.Home.Primitives;
using Momoka.Home.Levels.Entities.Properties;
using Momoka.Home.Levels.Entities.Components;
namespace Momoka.Home.Levels;

/// <summary>
/// A 3D region — a connected component of standable space (a room, a walkable
/// area), aggregated from the column spans of a <see cref="ColumnLayout{T}"/>.
/// Region ids are assigned per build (0 = unassigned) and are not stable across
/// rebuilds.
/// </summary>
public sealed class Region
{
    /// <summary>1-based region id in the owning <see cref="ColumnLayout{T}"/>.</summary>
    public int Id { get; }

    /// <summary>Inclusive axis-aligned bounds of all cells in the region.</summary>
    public Bound Bounds { get; }

    /// <summary>Total cells (each cell is 10 cm × 10 cm × 10 cm).</summary>
    public long Volume { get; }

    /// <summary>Distinct (x, z) columns in the region's footprint.</summary>
    public int Area { get; }

    /// <summary>Human-readable label to tell spaces apart (e.g. "Bedroom"); set by the caller.</summary>
    public string Name { get; set; }

    internal Region(int id, Bound bounds, long volume, int area)
    {
        Id = id;
        Bounds = bounds;
        Volume = volume;
        Area = area;
        Name = $"Region {id}";
    }

    /// <summary>
    /// Builds the region layer of a space: standing cells are the top surfaces of
    /// the entities' <see cref="PlacementLayoutSource"/> placement planes
    /// (Up-facing), mapped to root-absolute voxel coordinates. Occupancy maps
    /// each cell to <c>is_structural &amp;&amp; !is_open</c> — structure blocks,
    /// open portals (doors) pass. Column spans are labeled by connected
    /// components (vertical gap ≤ the agent's max climb height merges) and
    /// written into the returned <see cref="ColumnLayout{T}"/> as whole-span
    /// fills. Manual — call once at ingestion; the space's
    /// <see cref="VoxelLayout{T}.Bound"/> is derived from the entities when unset.
    /// </summary>
    public static ColumnLayout<Region> BuildLayout(LevelLayout unit, Agent? agent = null)
    {
        agent ??= Agent.Human;
        var bound = unit.Voxels.Bound;
        if (!bound.Valid)
        {
            bound = ComputeExtent(unit);
            unit.Voxels.Bound = bound;
        }
        if (!bound.Valid)
            return new ColumnLayout<Region>(_ => false);

        Func<Int3, bool> blocked = p =>
            unit.Voxels[p] is { } e &&
            e.GetValue<bool>(Property.IsImmutable) &&
            !e.GetValue<bool>(Property.IsOpen);

        var regions = new ColumnLayout<Region>(blocked) { Bound = bound };

        var standing = GetWalkableCells(unit).ToList();
        if (standing.Count == 0)
            return regions;

        var width = 0;
        var depth = 0;
        foreach (var cell in standing)
        {
            if (cell.X > width) width = cell.X;
            if (cell.Z > depth) depth = cell.Z;
        }
        width++;
        depth++;

        var byColumn = new Dictionary<int, List<int>>();
        foreach (var cell in standing)
        {
            var column = cell.Z * width + cell.X;
            if (!byColumn.TryGetValue(column, out var ys))
                byColumn[column] = ys = new List<int>();
            ys.Add(cell.Y);
        }
        foreach (var ys in byColumn.Values)
        {
            ys.Sort();
            for (var i = ys.Count - 1; i > 0; i--)
                if (ys[i] == ys[i - 1])
                    ys.RemoveAt(i);
        }

        var spans = new List<(int X, int Z, int Y0, int Y1)>();
        var colOf = new List<int>();
        var colStart = new List<int> { 0 };
        var maxY = bound.Max.Y;
        for (var z = 0; z < depth; z++)
            for (var x = 0; x < width; x++)
            {
                var column = z * width + x;
                if (byColumn.TryGetValue(column, out var ys))
                {
                    for (var i = 0; i < ys.Count; i++)
                    {
                        var y0 = ys[i];
                        var y = y0;
                        while (y <= maxY)
                        {
                            if (i + 1 < ys.Count && y >= ys[i + 1])
                                break;
                            if (blocked(new Int3(x, y, z)))
                                break;
                            y++;
                        }
                        if (y > y0)
                        {
                            spans.Add((x, z, y0, y));
                            colOf.Add(column);
                        }
                    }
                }
                colStart.Add(spans.Count);
            }

        var labelOf = new int[spans.Count];
        var nextLabel = 0;
        var maxClimb = agent.MaxClimbHeight;
        for (var i = 0; i < labelOf.Length; i++)
        {
            if (labelOf[i] != 0)
                continue;
            nextLabel++;
            labelOf[i] = nextLabel;
            var queue = new Queue<int>();
            queue.Enqueue(i);
            while (queue.Count > 0)
            {
                var cur = queue.Dequeue();
                var c = colOf[cur];
                var x = c % width;
                var z = c / width;
                foreach (var (nx, nz) in Neighbors(x, z, width, depth))
                {
                    var nc = nz * width + nx;
                    for (var k = colStart[nc]; k < colStart[nc + 1]; k++)
                    {
                        if (labelOf[k] != 0)
                            continue;
                        var gap = Math.Max(spans[cur].Y0, spans[k].Y0) - Math.Min(spans[cur].Y1, spans[k].Y1);
                        if (gap > maxClimb)
                            continue;
                        labelOf[k] = nextLabel;
                        queue.Enqueue(k);
                    }
                }
            }
        }

        var stats = new (int MinX, int MinY, int MinZ, int MaxX, int MaxY, int MaxZ, long Volume, HashSet<long> Footprint)[nextLabel + 1];
        for (var r = 1; r <= nextLabel; r++)
            stats[r] = (int.MaxValue, int.MaxValue, int.MaxValue, int.MinValue, int.MinValue, int.MinValue, 0L, new HashSet<long>());
        for (var k = 0; k < spans.Count; k++)
        {
            var r = labelOf[k];
            var (x, z, y0, y1) = spans[k];
            ref var s = ref stats[r];
            if (x < s.MinX) s.MinX = x;
            if (z < s.MinZ) s.MinZ = z;
            if (y0 < s.MinY) s.MinY = y0;
            if (x > s.MaxX) s.MaxX = x;
            if (z > s.MaxZ) s.MaxZ = z;
            if (y1 - 1 > s.MaxY) s.MaxY = y1 - 1;
            s.Volume += y1 - y0;
            s.Footprint.Add((long)z << 32 | (uint)x);
        }

        var regionById = new Region[nextLabel + 1];
        for (var r = 1; r <= nextLabel; r++)
        {
            var s = stats[r];
            var bounds = Bound.FromCorners(new Int3(s.MinX, s.MinY, s.MinZ).ToFloat3(), new Int3(s.MaxX, s.MaxY, s.MaxZ).ToFloat3());
            regionById[r] = new Region(r, bounds, s.Volume, s.Footprint.Count);
        }

        for (var k = 0; k < spans.Count; k++)
        {
            var (x, z, y0, y1) = spans[k];
            regions.SetSpan(x, y0, y1, z, regionById[labelOf[k]]);
        }
        return regions;
    }

    /// <summary>
    /// Standing cells: Up-facing placement surfaces of entities marked
    /// <see cref="Property.IsImmutable"/> (floors, stairs, yard ground),
    /// mapped to root-absolute voxel coordinates. Structural-and-closed entities
    /// block the engine's span scan (walls, closed doors); table tops / treadmill
    /// decks are excluded as non-structural.
    /// </summary>
    private static IEnumerable<Int3> GetWalkableCells(LevelLayout unit)
    {
        foreach (var entity in unit.Entities)
        {
            if (!(entity.TryGetValue(Property.IsImmutable, out var value) && value is true))
                continue;
            foreach (var source in entity.GetComponents<PlacementLayoutSource>())
            {
                var surface = source.Layout;
                if (surface is null || source.Transform.Rotation != Rotation.Up)
                    continue;
                for (var z = 0; z < surface.Size.Z; z++)
                    for (var x = 0; x < surface.Size.X; x++)
                    {
                        var rel = new Int2(x, z);
                        if (surface[rel])
                            yield return source.AsAbsolute(rel);
                    }
            }
        }
    }

    private static IEnumerable<(int X, int Z)> Neighbors(int x, int z, int width, int depth)
    {
        if (x > 0) yield return (x - 1, z);
        if (x + 1 < width) yield return (x + 1, z);
        if (z > 0) yield return (x, z - 1);
        if (z + 1 < depth) yield return (x, z + 1);
    }

    private static Bound ComputeExtent(LevelLayout unit)
    {
        var minX = float.MaxValue;
        var minY = float.MaxValue;
        var minZ = float.MaxValue;
        var maxX = float.MinValue;
        var maxY = float.MinValue;
        var maxZ = float.MinValue;
        var any = false;
        foreach (var entity in unit.Entities)
        {
            if (entity.Volume is null)
                continue;
            foreach (var cell in entity.Volume.GetVoxelSet())
            {
                var p = entity.Transform.Position + cell.ToFloat3();
                if (p.X < minX) minX = p.X;
                if (p.Y < minY) minY = p.Y;
                if (p.Z < minZ) minZ = p.Z;
                if (p.X > maxX) maxX = p.X;
                if (p.Y > maxY) maxY = p.Y;
                if (p.Z > maxZ) maxZ = p.Z;
                any = true;
            }
        }
        return any
            ? Bound.FromCorners(new Float3(minX, minY, minZ), new Float3(maxX, maxY, maxZ))
            : Bound.UnsetValue;
    }
}
