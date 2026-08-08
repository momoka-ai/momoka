using Momoka.Home.Components;
using Momoka.Home.Entities;
using Momoka.Home.Layouts;
using Momoka.Home.Primitives;
using Momoka.Home.Properties;
namespace Momoka.Home;

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
    /// (Up-facing), mapped to root-absolute voxel coordinates. Occupancy is the
    /// space's grid mapped to a boolean blocking grid via Select — the caller's
    /// mapping decides what blocks. The <see cref="ColumnLayout{T}.Build"/>
    /// engine labels connected spans, each span's value becoming its region.
    /// Manual — call once at ingestion; the space's
    /// <see cref="VoxelLayout{T}.Bound"/> is derived from the entities when
    /// unset.
    /// </summary>
    public static ColumnLayout<Region> BuildLayout(UnitLayout space, Agent? agent = null)
    {
        agent ??= Agent.Human;
        var bound = space.Layout.Bound;
        if (bound.IsEmpty)
        {
            bound = ComputeExtent(space);
            space.Layout.Bound = bound;
        }
        if (bound.IsEmpty)
            return ColumnLayout<Region>.Empty();

        var occupancy = space.Layout.Select(_ => true);
        var labels = ColumnLayout<int>.Build(
            occupancy,
            GetWalkableCells(space),
            new ColumnLayout<int>.Settings { MaxClimbHeight = agent.MaxClimbHeight });

        var regions = Aggregate(labels);
        return labels.Map(id => regions[id]);
    }

    /// <summary>
    /// Standing cells: Up-facing placement surfaces of entities marked
    /// <see cref="BuiltinProperty.IS_STRUCTURAL"/> (floors, stairs, yard ground),
    /// mapped to root-absolute voxel coordinates. Occupied cells are left to the
    /// engine's span scan — walls vanish because they occupy the standing cell
    /// itself; table tops / treadmill decks are excluded as non-structural.
    /// </summary>
    private static IEnumerable<Int3> GetWalkableCells(UnitLayout space)
    {
        foreach (var entity in space.Entities)
        {
            if (!(entity.TryGetValue(BuiltinProperty.IsStructural, out var value) && value is true))
                continue;
            foreach (var source in entity.GetComponents<PlacementLayoutSource>())
            {
                var surface = source.Layout;
                if (surface is null || surface.Direction != Int3.Up)
                    continue;
                for (var z = 0; z < surface.Size.Z; z++)
                    for (var x = 0; x < surface.Size.X; x++)
                    {
                        var rel = new Int2(x, z);
                        if (surface[rel])
                            yield return surface.AsAbsolute(rel);
                    }
            }
        }
    }

    private static Region[] Aggregate(ColumnLayout<int> labels)
    {
        var maxLabel = 0;
        foreach (var (_, _, span) in labels.AllSpans())
            maxLabel = Math.Max(maxLabel, span.Value);

        var minX = new int[maxLabel + 1];
        var minY = new int[maxLabel + 1];
        var minZ = new int[maxLabel + 1];
        var maxX = new int[maxLabel + 1];
        var maxY = new int[maxLabel + 1];
        var maxZ = new int[maxLabel + 1];
        Array.Fill(minX, int.MaxValue);
        Array.Fill(minY, int.MaxValue);
        Array.Fill(minZ, int.MaxValue);
        Array.Fill(maxX, int.MinValue);
        Array.Fill(maxY, int.MinValue);
        Array.Fill(maxZ, int.MinValue);
        var volume = new long[maxLabel + 1];
        var footprints = new HashSet<long>[maxLabel + 1];
        for (var r = 1; r <= maxLabel; r++)
            footprints[r] = new HashSet<long>();

        foreach (var (x, z, span) in labels.AllSpans())
        {
            var r = span.Value;
            if (x < minX[r]) minX[r] = x;
            if (z < minZ[r]) minZ[r] = z;
            if (span.Y0 < minY[r]) minY[r] = span.Y0;
            if (x > maxX[r]) maxX[r] = x;
            if (z > maxZ[r]) maxZ[r] = z;
            if (span.Y1 - 1 > maxY[r]) maxY[r] = span.Y1 - 1;
            volume[r] += span.Height;
            footprints[r].Add((long)z << 32 | (uint)x);
        }

        var regions = new Region[maxLabel + 1];
        for (var r = 1; r <= maxLabel; r++)
        {
            var bounds = Bound.FromCorners(
                new Int3(minX[r], minY[r], minZ[r]),
                new Int3(maxX[r], maxY[r], maxZ[r]));
            regions[r] = new Region(r, bounds, volume[r], footprints[r].Count);
        }
        return regions;
    }

    private static Bound ComputeExtent(UnitLayout space)
    {
        var minX = int.MaxValue;
        var minY = int.MaxValue;
        var minZ = int.MaxValue;
        var maxX = int.MinValue;
        var maxY = int.MinValue;
        var maxZ = int.MinValue;
        var any = false;
        foreach (var entity in space.Entities)
        {
            if (entity.Volume is null)
                continue;
            foreach (var cell in entity.Volume.Cells3D())
            {
                var p = entity.Coords + cell;
                if (p.X < minX) minX = p.X;
                if (p.Y < minY) minY = p.Y;
                if (p.Z < minZ) minZ = p.Z;
                if (p.X > maxX) maxX = p.X;
                if (p.Y > maxY) maxY = p.Y;
                if (p.Z > maxZ) maxZ = p.Z;
                any = true;
            }
        }
        return any ? Bound.FromCorners(new Int3(minX, minY, minZ), new Int3(maxX, maxY, maxZ)) : Bound.Empty;
    }
}
