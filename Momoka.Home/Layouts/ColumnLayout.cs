using System.Diagnostics.CodeAnalysis;
using Momoka.Home.Entities;
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
    public readonly struct Span
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
    /// Builds a label layout from the space's occupancy: <paramref name="cells"/>
    /// are the standing cells in root-absolute coordinates (pre-filtered by the
    /// caller — placement-surface tops with headroom). Each standing cell seeds a
    /// span that extends upward until the next standing cell, the first occupied
    /// cell, or the layout's <see cref="VoxelLayout{T}.Bound"/> top. Adjacent
    /// columns' spans merge when their vertical gap ≤
    /// <see cref="Settings.MaxClimbHeight"/> (4-connectivity in XZ). The output
    /// values are 1-based connected-component labels; use
    /// <see cref="Map{TOut}"/> to turn them into payloads. The layout's
    /// <see cref="VoxelLayout{T}.Bound"/> must be set (empty → empty layout).
    /// </summary>
    [SuppressMessage("Design", "CA1000:Do not declare static members on generic types",
        Justification = "Deliberate generic factory: the Build name and Settings belong on ColumnLayout.")]
    public static ColumnLayout<int> Build<TA>(
        VoxelLayout<TA> layout,
        IEnumerable<Int3> cells,
        ColumnLayout<T>.Settings settings)
        where TA : Entity
    {
        if (layout.Bound.IsEmpty)
            return new ColumnLayout<int>.Builder(1, 1).Build();

        var all = cells.ToList();
        if (all.Count == 0)
            return new ColumnLayout<int>.Builder(1, 1).Build();

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
                        if (layout[new Int3(x, y, z)] is not null)
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

        // ── 4. 打包 ──
        var builder = new ColumnLayout<int>.Builder(width, depth);
        for (var c = 0; c < width * depth; c++)
        {
            for (var k = colStart[c]; k < colStart[c + 1]; k++)
            {
                var s = spans[k];
                builder.AddSpan(s.Y0, s.Y1, labelOf[k]);
            }
            builder.NextColumn();
        }
        return builder.Build();
    }

    /// <summary>Remaps every span's value (e.g. labels → region references).</summary>
    public ColumnLayout<TOut> Map<TOut>(Func<T, TOut> map)
    {
        var builder = new ColumnLayout<TOut>.Builder(Width, Depth);
        for (var z = 0; z < Depth; z++)
            for (var x = 0; x < Width; x++)
            {
                var col = Column(x, z);
                for (var i = 0; i < col.Length; i++)
                    builder.AddSpan(col[i].Y0, col[i].Y1, map(col[i].Value));
                builder.NextColumn();
            }
        return builder.Build();
    }

    private static IEnumerable<(int X, int Z)> Neighbors(int x, int z, int width, int depth)
    {
        if (x > 0) yield return (x - 1, z);
        if (x + 1 < width) yield return (x + 1, z);
        if (z > 0) yield return (x, z - 1);
        if (z + 1 < depth) yield return (x, z + 1);
    }

    /// <summary>
    /// Streaming builder: feed columns in column-major order. Call
    /// <see cref="NextColumn"/> once after each column's spans; <see cref="Build"/>
    /// pads any trailing columns.
    /// </summary>
    public sealed class Builder
    {
        private readonly int _width;
        private readonly int _depth;
        private readonly List<Span> _spans = new();
        private readonly List<int> _offsets = new() { 0 };
        private int _columns;
        private int _columnStart;

        public Builder(int width, int depth)
        {
            if (width <= 0 || depth <= 0)
                throw new ArgumentOutOfRangeException(nameof(width), "Column layout requires positive width and depth.");
            _width = width;
            _depth = depth;
        }

        /// <summary>Finishes the current column and opens the next.</summary>
        public void NextColumn()
        {
            if (_columns >= _width * _depth)
                throw new InvalidOperationException($"Column layout already has all {_width * _depth} columns.");
            _offsets.Add(_spans.Count);
            _columns++;
            _columnStart = _spans.Count;
        }

        /// <summary>Appends a span to the current column; Y0 must be non-decreasing within the column.</summary>
        public void AddSpan(int y0, int y1, T value)
        {
            if (_spans.Count > _columnStart)
            {
                var prev = _spans[^1];
                if (y0 < prev.Y1)
                    throw new InvalidOperationException($"Column spans must be ascending and non-overlapping; got y0={y0} after [{prev.Y0}, {prev.Y1}).");
            }
            _spans.Add(new Span(y0, y1, value));
        }

        public ColumnLayout<T> Build()
        {
            while (_columns < _width * _depth)
                NextColumn();
            return new ColumnLayout<T>(_width, _depth, _spans.ToArray(), _offsets.ToArray());
        }
    }
}
