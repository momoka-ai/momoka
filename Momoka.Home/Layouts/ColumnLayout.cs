using System.Diagnostics.CodeAnalysis;
using Momoka.Home.Primitives;
namespace Momoka.Home.Layouts;

/// <summary>
/// A generic per-column interval layout: each (x, z) column has a fixed slot
/// holding a variable-length run of Y intervals (<see cref="Span"/>). Storage is
/// a flat contiguous span array plus a prefix-sum offset table (one entry per
/// column + 1), so a 10k-column footprint costs ~40 KB and two allocations
/// instead of 10k array objects. Columns are indexed by <c>z*Width + x</c> over
/// the inclusive XZ footprint; spans within a column are sorted by
/// <see cref="Span.Y0"/> and non-overlapping.
/// </summary>
public sealed class ColumnLayout<T>
{
    /// <summary>
    /// A half-open Y interval [<see cref="Y0"/>, <see cref="Y1"/>) carrying a
    /// value. Strongly coupled to <see cref="ColumnLayout{T}"/>, hence nested.
    /// </summary>
    public readonly struct Span : IEquatable<Span>
    {
        public readonly int Y0;
        public readonly int Y1;
        public readonly T Value;

        public Span(int y0, int y1, T value)
        {
            if (y1 <= y0)
                throw new ArgumentOutOfRangeException(nameof(y1), $"Span must be non-empty: [{y0}, {y1}).");
            Y0 = y0;
            Y1 = y1;
            Value = value;
        }

        /// <summary>Number of cells in the interval.</summary>
        public int Height => Y1 - Y0;

        /// <summary>True if <paramref name="y"/> lies in [Y0, Y1).</summary>
        public bool Contains(int y) => y >= Y0 && y < Y1;

        public bool Equals(Span other) =>
            Y0 == other.Y0 && Y1 == other.Y1 && EqualityComparer<T>.Default.Equals(Value, other.Value);

        public override bool Equals(object? obj) => obj is Span other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = Y0;
                hash = (hash * 397) ^ Y1;
                hash = (hash * 397) ^ (Value is null ? 0 : EqualityComparer<T>.Default.GetHashCode(Value));
                return hash;
            }
        }

        public static bool operator ==(Span a, Span b) => a.Equals(b);
        public static bool operator !=(Span a, Span b) => !a.Equals(b);

        public override string ToString() => $"[{Y0}, {Y1}) {Value}";
    }

    private readonly Span[] _spans;
    private readonly int[] _offsets;

    public int Width { get; }
    public int Depth { get; }

    /// <summary>Total columns = Width * Depth.</summary>
    public int ColumnCount => Width * Depth;

    /// <summary>Total spans across all columns.</summary>
    public int SpanCount => _spans.Length;

    internal ColumnLayout(int width, int depth, Span[] spans, int[] offsets)
    {
        Width = width;
        Depth = depth;
        _spans = spans;
        _offsets = offsets;
    }

    /// <summary>The column index for an XZ position inside the footprint.</summary>
    public int ColumnIndex(int x, int z) => z * Width + x;

    /// <summary>
    /// All spans of the column at (x, z), sorted by Y0 — zero-copy. Empty when
    /// the position is outside the footprint or the column has no spans.
    /// </summary>
    public ReadOnlySpan<Span> Column(int x, int z)
    {
        if ((uint)x >= (uint)Width || (uint)z >= (uint)Depth)
            return ReadOnlySpan<Span>.Empty;
        var c = ColumnIndex(x, z);
        return _spans.AsSpan(_offsets[c], _offsets[c + 1] - _offsets[c]);
    }

    /// <summary>The span containing <paramref name="y"/> in column (x, z), or null.</summary>
    public Span? Find(int x, int y, int z)
    {
        var col = Column(x, z);
        if (col.IsEmpty)
            return null;
        int lo = 0, hi = col.Length - 1;
        while (lo <= hi)
        {
            var mid = (lo + hi) >> 1;
            if (col[mid].Y0 <= y)
                lo = mid + 1;
            else
                hi = mid - 1;
        }
        return hi >= 0 && col[hi].Contains(y) ? col[hi] : null;
    }

    /// <summary>The value of the span containing <paramref name="y"/> in column (x, z); default(T) if none.</summary>
    public T At(int x, int y, int z) => Find(x, y, z) is { } span ? span.Value : default!;

    /// <summary>Enumerates every span with its column position.</summary>
    public IEnumerable<(int X, int Z, Span Span)> AllSpans()
    {
        for (var z = 0; z < Depth; z++)
            for (var x = 0; x < Width; x++)
            {
                var c = ColumnIndex(x, z);
                for (var i = _offsets[c]; i < _offsets[c + 1]; i++)
                    yield return (x, z, _spans[i]);
            }
    }

    /// <summary>
    /// Rules for building a label layout: connectivity tolerances between column
    /// spans, in cells (10 cm each). Defaults are human; map 1:1 from an
    /// <see cref="Agent"/>'s movement attributes.
    /// </summary>
    public sealed class Settings
    {
        /// <summary>Max vertical gap between adjacent columns' spans that still connects — the max step a unit climbs.</summary>
        public int MaxClimbHeight { get; init; } = 2;

        /// <summary>Max jump height — reserved for pathfinding.</summary>
        public int MaxJumpHeight { get; init; } = 6;
    }

    /// <summary>
    /// Builds a label layout from a boolean occupancy grid: <paramref name="cells"/>
    /// are the standing cells in root-absolute coordinates (pre-filtered by the
    /// caller — placement-surface tops with headroom); true cells block. Each
    /// standing cell seeds a span that extends upward until the next standing
    /// cell, the first blocked cell, or the layout's <see cref="VoxelLayout{T}.Bound"/>
    /// top. Adjacent columns' spans merge when their vertical gap ≤
    /// <see cref="Settings.MaxClimbHeight"/> (4-connectivity in XZ). The output
    /// values are 1-based connected-component labels; use
    /// <see cref="Map{TOut}"/> to turn them into payloads. The layout's
    /// <see cref="VoxelLayout{T}.Bound"/> must be set (empty → empty layout).
    /// </summary>
    [SuppressMessage("Design", "CA1000:Do not declare static members on generic types",
        Justification = "Deliberate: the Build name and Settings belong on ColumnLayout.")]
    public static ColumnLayout<int> Build(
        VoxelLayout<bool> layout,
        IEnumerable<Int3> cells,
        ColumnLayout<T>.Settings settings)
    {
        if (layout.Bound.IsEmpty)
            return ColumnLayout<int>.Empty();

        var all = cells.ToList();
        if (all.Count == 0)
            return ColumnLayout<int>.Empty();

        // ── 1. 站立格：按列分组，列内按 y 升序去重 ──
        var width = 0;
        var depth = 0;
        foreach (var cell in all)
        {
            if (cell.X > width) width = cell.X;
            if (cell.Z > depth) depth = cell.Z;
        }
        width++;
        depth++;

        var byColumn = new Dictionary<int, List<int>>();
        foreach (var cell in all)
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

        // ── 2. span：站立格向上，止于下一站立格 / 占用格 / Bound 顶 ──
        var spans = new List<ColumnLayout<int>.Span>();
        var colOf = new List<int>();
        var colStart = new List<int> { 0 };
        var maxY = layout.Bound.Max.Y;
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
                            if (layout[new Int3(x, y, z)])
                                break;
                            y++;
                        }
                        if (y > y0)
                        {
                            spans.Add(new ColumnLayout<int>.Span(y0, y, 0));
                            colOf.Add(column);
                        }
                    }
                }
                colStart.Add(spans.Count);
            }

        // ── 3. 连通标注：邻列 span 间距 ≤ MaxClimbHeight ──
        var labelOf = new int[spans.Count];
        var nextLabel = 0;
        var maxClimb = settings.MaxClimbHeight;
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

        // ── 4. 打包：colStart 已是前缀和偏移表 ──
        var packed = new ColumnLayout<int>.Span[spans.Count];
        for (var k = 0; k < spans.Count; k++)
            packed[k] = new ColumnLayout<int>.Span(spans[k].Y0, spans[k].Y1, labelOf[k]);
        return new ColumnLayout<int>(width, depth, packed, colStart.ToArray());
    }

    /// <summary>Remaps every span's value (e.g. labels → region references).</summary>
    public ColumnLayout<TOut> Map<TOut>(Func<T, TOut> map)
    {
        var packed = new ColumnLayout<TOut>.Span[_spans.Length];
        for (var i = 0; i < _spans.Length; i++)
            packed[i] = new ColumnLayout<TOut>.Span(_spans[i].Y0, _spans[i].Y1, map(_spans[i].Value));
        return new ColumnLayout<TOut>(Width, Depth, packed, _offsets);
    }

    private static IEnumerable<(int X, int Z)> Neighbors(int x, int z, int width, int depth)
    {
        if (x > 0) yield return (x - 1, z);
        if (x + 1 < width) yield return (x + 1, z);
        if (z > 0) yield return (x, z - 1);
        if (z + 1 < depth) yield return (x, z + 1);
    }

    /// <summary>A 1×1 layout with no spans (empty space / unbuildable).</summary>
    [SuppressMessage("Design", "CA1000:Do not declare static members on generic types",
        Justification = "Internal factory; see Build for the rationale.")]
    internal static ColumnLayout<T> Empty() =>
        new(1, 1, Array.Empty<Span>(), new int[2]); // offsets = [0, 0]
}
