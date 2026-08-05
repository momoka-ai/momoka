using Momoka.Home.Layouts;
using Momoka.Home.Primitives;
namespace Momoka.Home;

/// <summary>
/// The voxel-occupancy capability, decomposed out of <c>Entity&lt;Int3&gt;</c>: something
/// that occupies cells of a 3D voxel grid and can place/remove itself in a parent
/// layout. Shapes (leaf geometry) implement it with <see cref="Cells"/>; containers
/// (<c>Level</c>, <c>Building</c>) implement it by copying their own voxel layout
/// upward — the uniform upward-composition contract Home → Building → Level.
/// </summary>
public interface IVoxelShape3D
{
    /// <summary>Local occupied cells, relative to the placement origin.</summary>
    IEnumerable<Int3> Cells();

    /// <summary>Writes this object's occupied cells into <paramref name="target"/> at <paramref name="at"/>.</summary>
    void PlaceAt(VoxelLayout3D target, Int3 at);

    /// <summary>Removes this object's occupied cells from <paramref name="target"/> at <paramref name="at"/>.</summary>
    void DestroyAt(VoxelLayout3D target, Int3 at);
}
