using Momoka.Home.Geometry;
using Momoka.Home.Primitives;
namespace Momoka.Home.Layouts;

/// <summary>
/// A 2D planar grid — the 2D counterpart of <see cref="VoxelLayout{T}"/> (3D).
/// Contiguous storage over a fixed <see cref="Size"/>: a 200×200 plane is a
/// plain array, no chunking needed. Also the placement surface:
/// <see cref="Offset"/> and <see cref="Direction"/> position it in the parent
/// space, <see cref="AsAbsolute"/>/<see cref="AsRelative"/> map between layout
/// and world cells, and <see cref="IsCollided(Int2)"/>/<see cref="Fill"/> provide the
/// placement contract — <c>GridLayout&lt;bool&gt;</c> (true = placeable) is what
/// shelves, desktops and wall faces use.
/// </summary>
public class GridLayout<T> where T : notnull
{
    private readonly T[] _cells;

    /// <summary>Plane dimensions in cells.</summary>
    public Int2 Size { get; }

    /// <summary>Position of the layout plane's origin in the parent space (e.g. level-local coords).</summary>
    public Int3 Offset { get; set; }

    /// <summary>Normal direction of the surface (which way placed objects face).</summary>
    public Int3 Direction { get; set; } = Int3.Up;

    /// <summary>World length of one grid unit (cm) — this surface's own scale.</summary>
    public float UnitLength { get; set; } = 10f;

    public GridLayout(Int2 size, Int3? offset = null)
    {
        Size = size;
        Offset = offset ?? Int3.Zero;
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

    /// <summary>
    /// Maps a local layout cell to a world cell, based on <see cref="Direction"/>:
    /// Up/Down → XZ plane; East/West → YZ plane; North/South → XY plane.
    /// </summary>
    public Int3 AsAbsolute(Int2 rel)
    {
        if (Direction.X != 0) return Offset + new Int3(0, rel.X, rel.Z);
        if (Direction.Z != 0) return Offset + new Int3(rel.X, rel.Z, 0);
        return Offset + new Int3(rel.X, 0, rel.Z);
    }

    /// <summary>
    /// Inverse of <see cref="AsAbsolute"/>: projects a world cell onto this layout's
    /// plane, dropping the axis along <see cref="Direction"/>. This is the
    /// vertical/horizontal transform — a horizontal surface (Up/Down) keeps the
    /// object's XZ footprint (a 2×3×3 cabinet on the floor → 2×3), a wall
    /// (East/West) keeps YZ (→ 3×3, height×width), North/South keeps XY.
    /// </summary>
    public Int2 AsRelative(Int3 abs)
    {
        var rel = abs - Offset;
        if (Direction.X != 0) return new Int2(rel.Y, rel.Z);
        if (Direction.Z != 0) return new Int2(rel.X, rel.Y);
        return new Int2(rel.X, rel.Z);
    }

    private bool InBounds(Int2 c) =>
        c.X >= 0 && c.X < Size.X && c.Z >= 0 && c.Z < Size.Z;

    private static bool IsBlocked(T value) =>
        EqualityComparer<T>.Default.Equals(value, default);
}