using Momoka.Home.Entities;
using Momoka.Home.Layouts;
using Momoka.Home.Primitives;
namespace Momoka.Home.Geometry;

/// <summary>
/// The 3D voxel-occupancy contract: something that occupies cells of a 3D voxel
/// grid and can place/remove itself in a parent layout. Volumes (<see cref="Volume"/>)
/// and containers (<c>Level</c>, <c>Building</c>) implement it — containers by
/// copying their own voxel layout upward, the uniform upward-composition contract.
/// </summary>
public interface IVoxelGeometry3D
{
    /// <summary>Local occupied 3D cells, relative to the placement origin.</summary>
    IEnumerable<Int3> Cells3D();

    /// <summary>Writes this object's occupied cells into <paramref name="target"/> at <paramref name="at"/>.</summary>
    void PlaceAt(VoxelLayout<Entity<Int3>> target, Int3 at);

    /// <summary>Removes this object's occupied cells from <paramref name="target"/> at <paramref name="at"/>.</summary>
    void DestroyAt(VoxelLayout<Entity<Int3>> target, Int3 at);
}
