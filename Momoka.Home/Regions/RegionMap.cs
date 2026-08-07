using Momoka.Home.Entities;
using Momoka.Home.Layouts;
using Momoka.Home.Primitives;
namespace Momoka.Home.Regions;

/// <summary>
/// The region layer of a space: labels every column span of free cells with a
/// region id via span flood fill (4-connectivity in XZ + Y-interval
/// overlap/step tolerance), then aggregates per-region facts. Built once from a
/// <see cref="VoxelLayout{T}"/> snapshot; rebuilt manually when the structure
/// changes. Region id 0 = unassigned (a point with no free span).
/// </summary>
public sealed class RegionMap
{
    private readonly ColumnLayout<int> _layout;
    private readonly Region[] _regions;
    private readonly int _originX;
    private readonly int _originZ;

    /// <summary>The regions of the map, ids 1..n in assign order.</summary>
    public IReadOnlyList<Region> Regions { get; }

    public int Width => _layout.Width;
    public int Depth => _layout.Depth;

    private RegionMap(ColumnLayout<int> layout, Region[] regions, int originX, int originZ)
    {
        _layout = layout;
        _regions = regions;
        _originX = originX;
        _originZ = originZ;
        Regions = regions;
    }

    /// <summary>The region containing the cell, or null (blocked / outside / empty).</summary>
    public Region? RegionAt(Int3 p) => RegionAt(p.X, p.Y, p.Z);

    /// <summary>The region containing the cell, or null.</summary>
    public Region? RegionAt(int x, int y, int z)
    {
        var id = _layout.At(x - _originX, y, z - _originZ);
        return id == 0 ? null : _regions[id - 1];
    }

    /// <summary>The region id at the cell (0 = none).</summary>
    public int RegionIdAt(int x, int y, int z) => _layout.At(x - _originX, y, z - _originZ);

    /// <summary>
    /// Builds the region map from the space occupancy. The footprint spans the
    /// layout's <see cref="VoxelLayout{T}.Bound"/> when set, otherwise the
    /// occupied extent of all entities.
    /// </summary>
    public static RegionMap Build(VoxelLayout<Entity> layout, RegionRules? rules = null)
    {
        rules ??= new RegionRules();

        var bound = layout.Bound.IsEmpty ? ComputeExtent(layout) : layout.Bound;
        if (bound.IsEmpty)
            return new RegionMap(new ColumnLayout<int>.Builder(1, 1).Build(), Array.Empty<Region>(), 0, 0);

        var width = bound.SizeX;
        var depth = bound.SizeZ;
        var originX = bound.Min.X;
        var originZ = bound.Min.Z;
        var minY = bound.Min.Y;
        var maxY = bound.Max.Y;

        var blocking = new HashSet<Entity>(layout.Entities.Where(rules.IsBlocking));

        // ── 1. 栅格化：逐列扫描 y，抽出 free 连续段（span）──
        var spans = new List<ColumnLayout<int>.Span>();
        var colOf = new List<int>();
        var colStart = new List<int> { 0 };
        for (var z = 0; z < depth; z++)
        {
            for (var x = 0; x < width; x++)
            {
                var column = z * width + x;
                var y = minY;
                while (y <= maxY)
                {
                    if (IsFree(layout, blocking, originX + x, y, originZ + z))
                    {
                        var y0 = y;
                        while (y <= maxY && IsFree(layout, blocking, originX + x, y, originZ + z))
                            y++;
                        spans.Add(new ColumnLayout<int>.Span(y0, y, 0));
                        colOf.Add(column);
                    }
                    else
                    {
                        y++;
                    }
                }
                colStart.Add(spans.Count);
            }
        }

        // ── 2. span flood-fill：4 邻列 + 区间重叠 / 步高容差 ──
        var regionOf = new int[spans.Count];
        var nextRegion = 0;
        for (var i = 0; i < regionOf.Length; i++)
        {
            if (regionOf[i] != 0)
                continue;

            nextRegion++;
            regionOf[i] = nextRegion;
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
                        if (regionOf[k] != 0 || !Linked(spans[cur], spans[k], rules.MaxStep))
                            continue;
                        regionOf[k] = nextRegion;
                        queue.Enqueue(k);
                    }
                }
            }
        }

        // ── 3. 聚合每个 Region ──
        var regions = Aggregate(spans, colOf, regionOf, nextRegion, width, originX, originZ);

        // ── 4. 打包 ColumnLayout ──
        var builder = new ColumnLayout<int>.Builder(width, depth);
        for (var c = 0; c < width * depth; c++)
        {
            for (var k = colStart[c]; k < colStart[c + 1]; k++)
            {
                var s = spans[k];
                builder.AddSpan(s.Y0, s.Y1, regionOf[k]);
            }
            builder.NextColumn();
        }

        return new RegionMap(builder.Build(), regions, originX, originZ);
    }

    private static bool IsFree(VoxelLayout<Entity> layout, HashSet<Entity> blocking, int x, int y, int z)
    {
        var entity = layout[new Int3(x, y, z)];
        return entity is null || !blocking.Contains(entity);
    }

    /// <summary>Two spans in adjacent columns connect if they overlap or are within a step of each other.</summary>
    private static bool Linked(ColumnLayout<int>.Span a, ColumnLayout<int>.Span b, int maxStep)
    {
        var gap = Math.Max(a.Y0, b.Y0) - Math.Min(a.Y1, b.Y1);
        return gap <= maxStep;
    }

    private static IEnumerable<(int X, int Z)> Neighbors(int x, int z, int width, int depth)
    {
        if (x > 0) yield return (x - 1, z);
        if (x + 1 < width) yield return (x + 1, z);
        if (z > 0) yield return (x, z - 1);
        if (z + 1 < depth) yield return (x, z + 1);
    }

    private static Region[] Aggregate(
        List<ColumnLayout<int>.Span> spans,
        List<int> colOf,
        int[] regionOf,
        int regionCount,
        int width,
        int originX,
        int originZ)
    {
        var minX = new int[regionCount + 1];
        var minY = new int[regionCount + 1];
        var minZ = new int[regionCount + 1];
        var maxX = new int[regionCount + 1];
        var maxY = new int[regionCount + 1];
        var maxZ = new int[regionCount + 1];
        Array.Fill(minX, int.MaxValue);
        Array.Fill(minY, int.MaxValue);
        Array.Fill(minZ, int.MaxValue);
        Array.Fill(maxX, int.MinValue);
        Array.Fill(maxY, int.MinValue);
        Array.Fill(maxZ, int.MinValue);
        var volume = new long[regionCount + 1];
        var footprints = new HashSet<long>[regionCount + 1];
        for (var r = 1; r <= regionCount; r++)
            footprints[r] = new HashSet<long>();

        for (var i = 0; i < spans.Count; i++)
        {
            var r = regionOf[i];
            var s = spans[i];
            var c = colOf[i];
            var x = originX + c % width;
            var z = originZ + c / width;
            if (x < minX[r]) minX[r] = x;
            if (z < minZ[r]) minZ[r] = z;
            if (s.Y0 < minY[r]) minY[r] = s.Y0;
            if (x > maxX[r]) maxX[r] = x;
            if (z > maxZ[r]) maxZ[r] = z;
            if (s.Y1 - 1 > maxY[r]) maxY[r] = s.Y1 - 1;
            volume[r] += s.Height;
            footprints[r].Add((long)z << 32 | (uint)x);
        }

        var regions = new Region[regionCount];
        for (var r = 1; r <= regionCount; r++)
        {
            var bounds = Bound.FromCorners(
                new Int3(minX[r], minY[r], minZ[r]),
                new Int3(maxX[r], maxY[r], maxZ[r]));
            regions[r - 1] = new Region(r, bounds, volume[r], footprints[r].Count);
        }
        return regions;
    }

    private static Bound ComputeExtent(VoxelLayout<Entity> layout)
    {
        var minX = int.MaxValue;
        var minY = int.MaxValue;
        var minZ = int.MaxValue;
        var maxX = int.MinValue;
        var maxY = int.MinValue;
        var maxZ = int.MinValue;
        var any = false;
        foreach (var entity in layout.Entities)
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
