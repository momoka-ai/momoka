using Momoka.Home.Models.Entities;
using Momoka.Home.Primitives;

namespace Momoka.Home.Models.Layouts;

/// <summary>
/// A 3D voxel occupancy container: chunked paletted storage plus the entities it
/// contains. Owns the consistency between the cell grid and the entity list —
/// placement, removal, and queries all go through here, so the two can never
/// drift apart. The 3D counterpart of <see cref="VoxelLayout2D"/> (which is the
/// 2D placement surface on a plane).
/// </summary>
public class VoxelLayout3D
{
    public Int3 ChunkSize { get; } = new(20, 30, 20);

    /// <summary>Chunked paletted cell storage.</summary>
    public GridLayout3D<VoxelEntity> Cells { get; }

    /// <summary>All entities held by this space, kept in sync with <see cref="Cells"/>.</summary>
    public List<Entity> Entities { get; } = new();

    public VoxelLayout3D() => Cells = new GridLayout3D<VoxelEntity>(ChunkSize);

    /// <summary>Gets or sets the VoxelEntity at a local coordinate (auto-creates the chunk on write).</summary>
    public VoxelEntity? this[Int3 pos]
    {
        get => Cells[pos];
        set => Cells[pos] = value;
    }

    /// <summary>True if a VoxelEntity occupies the given position.</summary>
    public bool HasEntity(Int3 pos) => this[pos] is not null;

    /// <summary>
    /// True if <paramref name="entity"/> can be placed at <paramref name="pos"/>:
    /// the anchor is free and none of the entity's (local) shape voxels overlap
    /// an occupied cell. Shape is local, so <paramref name="pos"/> offsets it.
    /// </summary>
    public bool CanPlace(VoxelEntity entity, Int3 pos)
    {
        if (HasEntity(pos))
            return false;

        foreach (var cell in entity.Shape.GetVoxels())
        {
            if (HasEntity(pos + cell))
                return false;
        }
        return true;
    }

    /// <summary>Places the entity at <paramref name="pos"/> and registers it.</summary>
    public bool Place(VoxelEntity entity, Int3 pos)
    {
        if (!CanPlace(entity, pos))
            return false;

        entity.Coords = pos;
        this[pos] = entity;
        Entities.Add(entity);
        return true;
    }

    /// <summary>Removes the entity and clears the cells it owns.</summary>
    public bool Remove(VoxelEntity entity)
    {
        if (!Entities.Remove(entity))
            return false;

        foreach (var cell in entity.Shape.GetVoxels())
        {
            var pos = entity.Coords + cell;
            if (this[pos] == entity)
                this[pos] = null;
        }
        return true;
    }

    /// <summary>
    /// Returns all entities whose shape intersects the axis-aligned box
    /// <paramref name="min"/>–<paramref name="max"/> (inclusive). Drag-select.
    /// </summary>
    public List<VoxelEntity> GetEntitiesInBound(Int2 min, Int2 max)
    {
        var result = new List<VoxelEntity>();
        foreach (var entity in Entities.OfType<VoxelEntity>())
        {
            foreach (var loc in entity.Shape.GetVoxels())
            {
                var p = (entity.Coords + loc).Xz;
                if (p.X >= min.X && p.X <= max.X && p.Z >= min.Z && p.Z <= max.Z)
                {
                    result.Add(entity);
                    break;
                }
            }
        }
        return result;
    }

    /// <summary>All entities assignable to the specified type.</summary>
    public List<T> GetEntitiesOfType<T>() where T : Entity =>
        Entities.OfType<T>().ToList();

    /// <summary>The entity at the given position, or null.</summary>
    public VoxelEntity? GetEntityAtPoint(Int3 pos) => this[pos];

    /// <summary>Nearest entity by expanding spiral search, or null.</summary>
    public VoxelEntity? GetEntityAtNearest(Int3 pos)
    {
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

    /// <summary>Finds an entity by its unique Id across this space.</summary>
    public Entity? FindEntity(Guid id) =>
        Entities.FirstOrDefault(e => e.Id == id);
}
