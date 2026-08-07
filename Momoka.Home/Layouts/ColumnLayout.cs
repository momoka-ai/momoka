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
