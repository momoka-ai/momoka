using Momoka.Home.Models.Shapes;
using Momoka.Home.Primitives;

namespace Momoka.Home.Models.Layouts;

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

    /// <summary>Offset of the layout plane relative to its host entity.</summary>
    public Int3 Offset { get; set; }

    /// <summary>Normal direction of the surface (which way placed objects face).</summary>
    public Int3 Direction { get; set; } = Int3.Up;

    /// <summary>
    /// Maps a local layout cell to a world cell, based on <see cref="Direction"/>:
    /// Up/Down → XZ plane; East/West → YZ plane; North/South → XY plane.
    /// </summary>
    public Int3 ToWorld(Int2 local)
    {
        if (Direction.X != 0) return Offset + new Int3(0, local.X, local.Z);
        if (Direction.Z != 0) return Offset + new Int3(local.X, local.Z, 0);
        return Offset + new Int3(local.X, 0, local.Z);
    }

    /// <summary>
    /// Inverse of <see cref="ToWorld"/>: projects a world cell onto this layout's
    /// plane, dropping the axis along <see cref="Direction"/>. This is the
    /// vertical/horizontal transform — a horizontal surface (Up/Down) keeps the
    /// object's XZ footprint (a 2×3×3 cabinet on the floor → 2×3), a wall
    /// (East/West) keeps YZ (→ 3×3, height×width), North/South keeps XY.
    /// </summary>
    public Int2 ToLocal(Int3 world)
    {
        var rel = world - Offset;
        if (Direction.X != 0) return new Int2(rel.Y, rel.Z);
        if (Direction.Z != 0) return new Int2(rel.X, rel.Y);
        return new Int2(rel.X, rel.Z);
    }

    /// <summary>True if the local cell allows placement (in bounds and not blocked).</summary>
    public bool IsCollided(Int2 xzCoords) => this[xzCoords];

    /// <summary>
    /// True if any of the shape's support-footprint cells — as returned by
    /// <see cref="Shape.GetVoxelsOnAngle"/>, expressed in this layout's local
    /// frame — lands on a blocked (or out-of-bounds) cell. The placement
    /// transforms the object's footprint onto the surface plane (using
    /// <see cref="ToLocal"/> for the vertical/horizontal case) before querying.
    /// </summary>
    public bool IsCollided(Shape shape)
    {
        foreach (var cell in shape.GetVoxelsOnAngle())
        {
            if (!this[cell])
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
    IReadOnlyList<VoxelLayout2D> Layouts { get; }
}
