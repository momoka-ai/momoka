using Momoka.Home.Primitives;
namespace Momoka.Home.Layouts;

public sealed class ColumnLayout<T> where T : notnull
{
    private readonly VoxelLayout<T> _cells = new();
    private readonly Func<Int3, bool> _isBlocked;

    public ColumnLayout(Func<Int3, bool> isBlocked) => _isBlocked = isBlocked;

    public Bound Bound
    {
        get => _cells.Bound;
        set => _cells.Bound = value;
    }

    public T At(int x, int y, int z) => _cells[new Int3(x, y, z)] ?? default!;

    public IEnumerable<(Int3 Position, T Value)> Cells() => _cells.Cells();

    public void SetAt(int x, int y, int z, T value)
    {
        var y0 = y;
        while (y0 > 0 && !_isBlocked(new Int3(x, y0 - 1, z)))
            y0--;
        var y1 = y + 1;
        var top = _cells.Bound.Valid ? _cells.Bound.Max.Y + 1 : y1;
        while (y1 < top && !_isBlocked(new Int3(x, y1, z)))
            y1++;
        SetSpan(x, y0, y1, z, value);
    }

    public void SetSpan(int x, int y0, int y1, int z, T value)
    {
        for (var y = y0; y < y1; y++)
            _cells[new Int3(x, y, z)] = value;
    }
}
