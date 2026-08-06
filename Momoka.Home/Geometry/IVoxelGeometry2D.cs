using Momoka.Home.Primitives;
namespace Momoka.Home.Geometry;

/// <summary>
/// The 2D voxel-occupancy contract: something that enumerates cells in the local
/// XZ plane. <see cref="Shape"/> (footprints) and <see cref="Volume"/>
/// (its projection onto the plane) implement it.
/// </summary>
public interface IVoxelGeometry2D
{
    /// <summary>Local occupied 2D cells, relative to the placement origin.</summary>
    IEnumerable<Int2> Cells2D();
}
