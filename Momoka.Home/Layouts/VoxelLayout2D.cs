using Momoka.Home.Primitives;

namespace Momoka.Home;

/// <summary>
/// A 2D placement layout on an entity's surface: a planar grid (inherits
/// <see cref="GridLayout2D{T}"/>) whose boolean cells mark where objects can be
/// placed (true = placeable, false/empty = blocked). The layout lives in the
/// surface's local plane; <see cref="Offset"/> and <see cref="Direction"/> position
/// and orient it relative to the host entity. A shelf, a desktop, or a wall
/// face is one such layout.
/// </summary>
public class VoxelLayout2D : GridLayout2D<bool>
{
    public VoxelLayout2D(Int2 size, Int3? offset = null) : base(size)
    {
        Offset = offset ?? Int3.Zero;
    }

    /// <summary>Position of the layout plane's origin in the parent space (e.g. level-local coords).</summary>
    public Int3 Offset { get; set; }

    /// <summary>Normal direction of the surface (which way placed objects face).</summary>
    public Int3 Direction { get; set; } = Int3.Up;

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

    /// <summary>True if the local cell is blocked (cannot be placed there).</summary>
    public bool IsCollided(Int2 xzCoords) => !this[xzCoords];

    /// <summary>
    /// True if the shape's support footprint, placed at layout-local
    /// <paramref name="pos"/>, lands on any blocked (or out-of-bounds) cell.
    /// The footprint cells come from <see cref="Shape.GetVoxelsOnAngle"/> and are
    /// local to the object — the object's position on this surface is added here.
    /// </summary>
    public bool IsCollided(Shape shape, Int2 pos)
    {
        foreach (var cell in shape.GetVoxelsOnAngle())
        {
            if (!this[cell + pos])
                return true;
        }
        return false;
    }

    /// <summary>Marks a rectangle of cells (in local coords) as placeable.</summary>
    public void Fill(Int2 from, Int2 size)
    {
        for (var dx = 0; dx < size.X; dx++)
            for (var dz = 0; dz < size.Z; dz++)
                this[new Int2(from.X + dx, from.Z + dz)] = true;
    }
}

/// <summary>
/// Capability of an entity to provide one or more 2D placement layouts
/// (surfaces) that can host placed objects — a wall (two faces), a floor, or a
/// bookshelf (one layout per shelf). Placement attaches to a layout and the
/// surface's cells define where objects may rest.
/// </summary>
public interface IVoxelLayout2DSource
{
    IEnumerable<VoxelLayout2D> Layouts { get; }
}
