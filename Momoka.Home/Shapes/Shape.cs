using Momoka.Home;
using Momoka.Home.Entities;
using Momoka.Home.Layouts;
using Momoka.Home.Primitives;
namespace Momoka.Home.Shapes;

/// <summary>
/// The geometry of a body's voxel occupancy — local cells relative to the host's
/// Coords. Shapes carry no position (that lives on the host entity); they only
/// describe their own shape. Implements <see cref="IVoxelShape3D"/> as the leaf
/// geometry provider; placement into an entity grid goes through the host
/// (<c>VoxelLayout3D.BuildAt(Entity&lt;Int3&gt;, pos)</c>), while containers
/// (<see cref="Level"/>, <see cref="Building"/>) implement the interface for
/// upward composition.
/// </summary>
public abstract class Shape : IVoxelShape3D
{
    /// <summary>Local occupied cells, relative to the host's Coords (snapped to the grid).</summary>
    public abstract IEnumerable<Int3> Cells();

    /// <summary>
    /// Support footprint: the shape projected onto its local XZ plane — the face
    /// that contacts the placement surface.
    /// </summary>
    public abstract IEnumerable<Int2> GetVoxelsOnAngle();

    /// <summary>
    /// A bare shape has no host identity, so it cannot place itself into an entity
    /// grid. Place the host instead: <c>VoxelLayout3D.BuildAt(Entity&lt;Int3&gt;, pos)</c>.
    /// Containers (<see cref="Level"/>, <see cref="Building"/>) implement real
    /// PlaceAt/DestroyAt for upward composition.
    /// </summary>
    public void PlaceAt(VoxelLayout3D target, Int3 at) =>
        throw new NotSupportedException("A bare shape has no host identity — place it via VoxelLayout3D.BuildAt(Entity<Int3>, pos) or via a container (Level/Building).");

    /// <inheritdoc cref="PlaceAt"/>
    public void DestroyAt(VoxelLayout3D target, Int3 at) =>
        throw new NotSupportedException("A bare shape has no host identity — remove it via VoxelLayout3D.DestroyAt(pos) or via a container (Level/Building).");
}
