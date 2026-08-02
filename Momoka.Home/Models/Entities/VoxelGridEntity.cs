using Momoka.Home.Models.Layouts;
using Momoka.Home.Models.Levels;
using Momoka.Home.Primitives;

namespace Momoka.Home.Models.Entities;

/// <summary>
/// An <see cref="VoxelEntity"/> that is itself a spatial volume of blocks — a 3D
/// grid backed by chunked paletted storage. The grid owns a local coordinate
/// system; <see cref="Origin"/> offsets it relative to its parent composition,
/// so an assembled space (a building, the yard) can move as a whole without
/// re-keying its contents. <see cref="Level"/> (a floor) and Home (the yard)
/// are both block compositions.
/// </summary>
public class VoxelGridEntity : VoxelEntity
{
    public Int3 ChunkSize { get; } = new(20, 30, 20);
    /// <summary>
    /// Inclusive world-space footprint of this space. Its position relative to
    /// the enclosing level is inherited from <see cref="VoxelEntity.Coords"/>.
    /// </summary>
    public Bound Bound { get; set; } = Bound.Empty;
    public List<Entity> Entities { get; } = new();

    private readonly GridLayout3D<VoxelEntity> _innerLayout;

    public VoxelGridEntity()
    {
        _innerLayout = new(ChunkSize);
    }

    /// <summary>
    /// Gets or sets the VoxelEntity at a local coordinate of this grid
    /// (world position minus <see cref="VoxelEntity.Coords"/>).
    /// Auto-creates the containing chunk on write.
    /// </summary>
    public VoxelEntity? this[Int3 coords]
    {
        get => _innerLayout[coords];
        set => _innerLayout[coords] = value;
    }

    /// <summary>Returns true if a VoxelEntity occupies the given position.</summary>
    public bool HasEntity(Int3 coords) => this[coords] is not null;

    /// <summary>
    /// Returns all VoxelEntity instances whose shape intersects the axis-aligned bounding box
    /// defined by <paramref name="min"/> and <paramref name="max"/> (inclusive).
    /// Useful for drag-select in the editor.
    /// </summary>
    public List<VoxelEntity> GetEntitiesInRegion(Int2 min, Int2 max)
    {
        var result = new List<VoxelEntity>();
        foreach (var entity in Entities.OfType<VoxelEntity>())
        {
            foreach (var loc in entity.Shape.Locations())
            {
                var p = loc.Int2;
                if (p.X >= min.X && p.X <= max.X && p.Z >= min.Z && p.Z <= max.Z)
                {
                    result.Add(entity);
                    break;
                }
            }
        }
        return result;
    }

    /// <summary>
    /// Returns all entities in this space that are assignable to the specified type.
    /// </summary>
    public List<T> GetEntitiesOfType<T>() where T : Entity =>
        Entities.OfType<T>().ToList();

    /// <summary>
    /// Returns the VoxelEntity at the given integer grid position, or null if empty.
    /// </summary>
    public VoxelEntity? GetEntityAtPoint(Int3 pos) => this[pos];

    /// <summary>
    /// Finds the nearest VoxelEntity to <paramref name="pos"/> by expanding spiral search.
    /// Returns null if the space has no BlockEntities.
    /// </summary>
    public VoxelEntity? GetEntityAtNearest(Int3 pos)
    {
        // Spiral search — expand radius until an entity is found
        for (var radius = 0; radius < 1000; radius++)
        {
            for (var dx = -radius; dx <= radius; dx++)
            {
                for (var dz = -radius; dz <= radius; dz++)
                {
                    if (Math.Abs(dx) != radius && Math.Abs(dz) != radius)
                        continue;

                    var candidate = this[new Int3(pos.X + dx, pos.Y, pos.Z + dz)];
                    if (candidate is not null)
                        return candidate;
                }
            }
        }
        return null;
    }

    /// <summary>
    /// Finds an entity by its unique Id across this space. Returns null if not found.
    /// </summary>
    public Entity? FindEntity(Guid id) =>
        Entities.FirstOrDefault(e => e.Id == id);
}
