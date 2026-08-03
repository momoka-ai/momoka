using Momoka.Home.Primitives;

namespace Momoka.Home;

/// <summary>
/// A 3D voxel occupancy container: chunked paletted storage (inherits
/// <see cref="GridLayout3D{T}"/>) plus the entities it holds. Owns the
/// consistency between the cell grid and the entity list — every construction
/// and destruction writes/clears ALL of the entity's voxels, so the two can
/// never drift apart. The 3D counterpart of <see cref="VoxelLayout2D"/> (the 2D
/// placement surface on a plane).
/// </summary>
public class VoxelLayout3D : GridLayout3D<VoxelEntity>
{
    /// <summary>All entities held by this space, kept in sync with the cell grid.</summary>
    public List<Entity> Entities { get; } = new();

    public VoxelLayout3D(Int3? chunkSize = null) : base(chunkSize ?? new Int3(20, 30, 20))
    {
    }

    /// <summary>True if a VoxelEntity occupies the given position.</summary>
    public bool HasEntity(Int3 pos) => this[pos] is not null;

    /// <summary>
    /// True if placing <paramref name="entity"/> at <paramref name="cs"/> would
    /// collide: the anchor or any of its (local) shape voxels lands on an
    /// occupied cell.
    /// </summary>
    public bool IsEntityCollided(VoxelEntity entity, Int3 cs)
    {
        if (HasEntity(cs))
            return true;

        foreach (var cell in entity.Shape.GetVoxels())
        {
            if (HasEntity(cs + cell))
                return true;
        }
        return false;
    }

    /// <summary>
    /// True if placing <paramref name="src"/> at <paramref name="cs"/> intersects
    /// the specific <paramref name="dest"/> entity (dest voxels vs src voxels).
    /// </summary>
    public bool IsEntityCollided(VoxelEntity dest, VoxelEntity src, Int3 cs)
    {
        var destCells = dest.Shape.GetVoxels()
            .Select(v => dest.Coords + v)
            .ToHashSet();
        return src.Shape.GetVoxels().Any(v => destCells.Contains(cs + v));
    }

    /// <summary>
    /// Constructs (places) the entity at <paramref name="cs"/>: writes EVERY one
    /// of its shape voxels into the grid and registers it. False if collided.
    /// </summary>
    public bool ConstructAt(VoxelEntity entity, Int3 cs)
    {
        if (IsEntityCollided(entity, cs))
            return false;

        entity.Coords = cs;
        foreach (var cell in entity.Shape.GetVoxels())
        {
            this[cs + cell] = entity;
        }
        Entities.Add(entity);
        return true;
    }

    /// <summary>
    /// Undoes <see cref="ConstructAt"/> at the entity's registered position:
    /// removes the entity whose Coords equals <paramref name="pos"/>.
    /// </summary>
    public bool DestructAt(Int3 pos)
    {
        var entity = Entities.OfType<VoxelEntity>().FirstOrDefault(e => e.Coords == pos);
        return entity is not null && Remove(entity);
    }

    /// <summary>Removes the entity covering the given target cell.</summary>
    public bool DestructTarget(Int3 target)
    {
        if (this[target] is not VoxelEntity entity)
            return false;
        return Remove(entity);
    }

    /// <summary>
    /// Clears the current storage and re-rasterizes every held VoxelEntity into
    /// the grid — a forced flush/refresh after direct low-level cell writes.
    /// </summary>
    public void FlushVoxelEntities()
    {
        Clear();
        foreach (var entity in Entities.OfType<VoxelEntity>())
        {
            foreach (var cell in entity.Shape.GetVoxels())
            {
                this[entity.Coords + cell] = entity;
            }
        }
    }

    private bool Remove(VoxelEntity entity)
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
