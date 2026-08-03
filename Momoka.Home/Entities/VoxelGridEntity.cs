using Momoka.Home.Primitives;

namespace Momoka.Home;

/// <summary>
/// An <see cref="VoxelEntity"/> that is itself a spatial volume of blocks — a
/// composition space (a building, a floor, the yard). All grid storage and
/// occupancy operations live in the composed <see cref="VoxelLayout3D"/>
/// (<see cref="Layout"/>); the entity itself only adds identity,
/// <see cref="VoxelEntity.Coords"/>, and a footprint (<see cref="Bound"/>).
/// <see cref="Level"/> (a floor) and Home (the yard) are both such spaces.
/// </summary>
public class VoxelGridEntity : VoxelEntity
{
    /// <summary>The voxel occupancy container backing this space.</summary>
    public VoxelLayout3D Layout { get; } = new();

    /// <summary>
    /// Inclusive world-space footprint of this space. Its position relative to
    /// the enclosing composition is inherited from <see cref="VoxelEntity.Coords"/>.
    /// </summary>
    public Bound Bound { get; set; } = Bound.Empty;
}
