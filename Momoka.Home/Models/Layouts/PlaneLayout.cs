using Momoka.Home.Primitives;

namespace Momoka.Home.Models.Layouts;

/// <summary>
/// A large planar surface — a floor, a ceiling — combining:
///  • the placement surface itself (the <see cref="VoxelLayout2D"/> base: which
///    cells objects may occupy on the plane),
///  • a planar <see cref="Subdivision{T}"/> of material/region faces covering
///    the plane (wood / tile / carpet zones).
///
/// A plane is a SINGLE layer. Stacked attachment surfaces (bookshelves, hanging
/// fixtures at different heights) are a 3D occupancy concern — model them with
/// <see cref="VoxelLayout3D"/> or as separate layouts, not as plane layers.
/// </summary>
public class PlaneLayout<T> : VoxelLayout2D where T : class
{
    /// <summary>Material / region faces covering this plane.</summary>
    public Subdivision<T> Subdivision { get; } = new();

    public PlaneLayout(Int2 size, Int3? offset = null) : base(size, offset)
    {
    }
}
