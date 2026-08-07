using Momoka.Home;
using Momoka.Home.Entities;
using Momoka.Home.Layouts;
using Momoka.Home.Primitives;
namespace Momoka.Home.Geometry;

/// <summary>
/// The 3D counterpart of <see cref="Shape"/>: a volume occupying voxel cells in
/// local 3D space, relative to the host's Coords. Volumes carry no position (that
/// lives on the host entity); they only describe their own geometry. Implements
/// <see cref="IVoxelGeometry3D"/> (its own 3D cells) and
/// <see cref="IVoxelGeometry2D"/> (its projection onto the local XZ plane).
/// Placement into an entity grid goes through the host
/// (<c>VoxelLayout&lt;Entity&lt;Int3&gt;&gt;.BuildAt(Entity&lt;Int3&gt;, pos)</c>), while containers
/// (<see cref="Level"/>, <see cref="Building"/>) implement the interfaces for
/// upward composition.
/// </summary>
public abstract class Volume : IVoxelGeometry3D, IVoxelGeometry2D
{
    /// <summary>Local occupied 3D cells, relative to the host's Coords (snapped to the grid).</summary>
    public abstract IEnumerable<Int3> Cells3D();

    /// <summary>
    /// The volume's 2D projection — its support footprint on the local XZ plane,
    /// the face that contacts the placement surface.
    /// </summary>
    public abstract IEnumerable<Int2> Cells2D();

    /// <summary>
    /// A bare volume has no host identity, so it cannot place itself into an entity
    /// grid. Place the host instead: <c>VoxelLayout&lt;Entity&lt;Int3&gt;&gt;.BuildAt(Entity&lt;Int3&gt;, pos)</c>.
    /// Containers (<see cref="Level"/>, <see cref="Building"/>) implement real
    /// PlaceAt/DestroyAt for upward composition.
    /// </summary>
    public void PlaceAt(VoxelLayout<Entity<Int3>> target, Int3 at) =>
        throw new NotSupportedException("A bare volume has no host identity — place it via VoxelLayout&lt;Entity&lt;Int3&gt;&gt;.BuildAt(Entity&lt;Int3&gt;, pos) or via a container (Level/Building).");

    /// <inheritdoc cref="PlaceAt"/>
    public void DestroyAt(VoxelLayout<Entity<Int3>> target, Int3 at) =>
        throw new NotSupportedException("A bare volume has no host identity — remove it via VoxelLayout&lt;Entity&lt;Int3&gt;&gt;.DestroyAt(pos) or via a container (Level/Building).");
}
