using Momoka.Home.Models.Layouts;
using Momoka.Home.Primitives;

namespace Momoka.Home.Models.Entities;

/// <summary>
/// An <see cref="VoxelEntity"/> that is itself a spatial volume of blocks — a
/// composition space (a building, a floor, the yard). The grid storage and
/// occupancy logic live in a composed <see cref="VoxelLayout3D"/>; the entity
/// adds identity, <see cref="VoxelEntity.Coords"/>, a footprint
/// (<see cref="Bound"/>), and the ability to be placed into a parent grid.
/// <see cref="Level"/> (a floor) and Home (the yard) are both such spaces.
/// </summary>
public class VoxelGridEntity : VoxelEntity
{
    /// <summary>The voxel occupancy container backing this space.</summary>
    public VoxelLayout3D Layout { get; } = new();

    public Int3 ChunkSize => Layout.ChunkSize;

    /// <summary>
    /// Inclusive world-space footprint of this space. Its position relative to
    /// the enclosing composition is inherited from <see cref="VoxelEntity.Coords"/>.
    /// </summary>
    public Bound Bound { get; set; } = Bound.Empty;

    public List<Entity> Entities => Layout.Entities;

    /// <summary>Gets or sets the VoxelEntity at a local coordinate of this space.</summary>
    public VoxelEntity? this[Int3 coords]
    {
        get => Layout[coords];
        set => Layout[coords] = value;
    }

    /// <summary>True if a VoxelEntity occupies the given position.</summary>
    public bool HasEntity(Int3 coords) => Layout.HasEntity(coords);

    public bool CanPlace(VoxelEntity entity, Int3 pos) => Layout.CanPlace(entity, pos);

    public bool Place(VoxelEntity entity, Int3 pos) => Layout.Place(entity, pos);

    public bool Remove(VoxelEntity entity) => Layout.Remove(entity);

    public List<VoxelEntity> GetEntitiesInBound(Int2 min, Int2 max) =>
        Layout.GetEntitiesInBound(min, max);

    public List<T> GetEntitiesOfType<T>() where T : Entity =>
        Layout.GetEntitiesOfType<T>();

    public VoxelEntity? GetEntityAtPoint(Int3 pos) => Layout.GetEntityAtPoint(pos);

    public VoxelEntity? GetEntityAtNearest(Int3 pos) => Layout.GetEntityAtNearest(pos);

    public Entity? FindEntity(Guid id) => Layout.FindEntity(id);
}
