using Momoka.Home.Geometry;
using Momoka.Home.Primitives;
namespace Momoka.Home.Layouts;

/// <summary>
/// A 2D planar grid — the 2D counterpart of <see cref="VoxelLayout{T}"/> (3D).
/// Contiguous storage over a fixed <see cref="Size"/>: a 200×200 plane is a
/// plain array, no chunking needed. **纯局部格网数据**——不含位置 / 朝向
/// （表面姿态由宿主 <c>PlacementLayoutSource.Transform</c> 承载，本类型只管
/// 格网本身：Size + UnitLength + cells 与局部判定）。
/// </summary>
public class GridLayout<T> where T : notnull
{
    private readonly T[] _cells;

    /// <summary>Plane dimensions in cells.</summary>
    public Int2 Size { get; }

    /// <summary>World length of one grid unit (cm) — this surface's own scale.</summary>
    public float UnitLength { get; set; } = 10f;

    public GridLayout(Int2 size)
    {
        Size = size;
        _cells = new T[size.X * size.Z];
    }

    /// <summary>Cell access; out-of-bounds reads return default(T), writes are ignored.</summary>
    public T this[Int2 coords]
    {
        get => InBounds(coords) ? _cells[coords.Z * Size.X + coords.X] : default!;
        set
        {
            if (InBounds(coords))
                _cells[coords.Z * Size.X + coords.X] = value;
        }
    }

    public void Clear() => Array.Clear(_cells);

    /// <summary>True if the local cell is blocked (cannot be placed there) — the cell is default(T).</summary>
    public bool IsCollided(Int2 xzCoords) => IsBlocked(this[xzCoords]);

    /// <summary>
    /// True if the shape's support footprint, placed at layout-local
    /// <paramref name="pos"/>, lands on any blocked (or out-of-bounds) cell.
    /// The footprint cells come from <see cref="IVoxelGeometry2D.Cells2D"/> and are
    /// local to the object — the object's position on this surface is added here.
    /// </summary>
    public bool IsCollided(IVoxelGeometry2D shape, Int2 pos)
    {
        foreach (var cell in shape.Cells2D())
        {
            if (IsBlocked(this[cell + pos]))
                return true;
        }
        return false;
    }

    /// <summary>Marks a rectangle of cells (in local coords) with <paramref name="value"/>.</summary>
    public void Fill(T value, Int2 from, Int2 size)
    {
        for (var dx = 0; dx < size.X; dx++)
            for (var dz = 0; dz < size.Z; dz++)
                this[new Int2(from.X + dx, from.Z + dz)] = value;
    }

    private bool InBounds(Int2 c) =>
        c.X >= 0 && c.X < Size.X && c.Z >= 0 && c.Z < Size.Z;

    private static bool IsBlocked(T value) =>
        EqualityComparer<T>.Default.Equals(value, default);
}
