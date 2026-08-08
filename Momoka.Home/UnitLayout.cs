using Momoka.Home.Components;
using Momoka.Home.Entities;
using Momoka.Home.Geometry;
using Momoka.Home.Layouts;
using Momoka.Home.Primitives;
namespace Momoka.Home;

/// <summary>
/// A unit of living space: the fully-3D, multi-layer spatial root of a
/// residence — the final form of space. Everything — floor slabs, ceilings,
/// walls, furniture, yard objects — is an <see cref="Entity"/> placed in the
/// single root grid, so placement and collision run directly against one root
/// space with root-absolute coordinates (no nested offset chains). UnitLayout
/// owns the entity list and the placement operations; <see cref="Layout"/> is
/// the pure cell grid underneath. Space semantics — rooms / walkable areas —
/// are the <see cref="Regions"/> layer (replacing the retired floor-plan
/// graphs).
/// </summary>
public sealed class UnitLayout : IEntitySource, IVoxelGeometry3D
{
    /// <summary>The pure 3D voxel occupancy grid: the single root space.</summary>
    public VoxelLayout<Entity> Layout { get; } = new();

    /// <summary>All entities of the space, kept in sync with the cell grid.</summary>
    public List<Entity> Entities { get; } = new();

    IReadOnlyList<Entity> IEntitySource.Entities => Entities;

    /// <summary>
    /// The 3D region layer (rooms / walkable areas) of the space — a
    /// <see cref="ColumnLayout{T}"/> of <see cref="Region"/> spans, built on
    /// demand. Null until <see cref="RebuildRegions"/> has run.
    /// </summary>
    public ColumnLayout<Region>? Regions { get; private set; }

    /// <summary>
    /// (Re)builds the region layer from the current occupancy and placement
    /// surfaces. Manual — call once at model ingestion; furniture
    /// placement/removal does not invalidate it. Structural edits should trigger
    /// a full scene rebuild.
    /// </summary>
    public ColumnLayout<Region> RebuildRegions(Agent? agent = null)
    {
        Regions = Region.BuildLayout(this, agent);
        return Regions;
    }

    /// <summary>The region containing the cell, or null (blocked / outside / unbuilt).</summary>
    public Region? RegionAt(Int3 p) => Regions?.At(p.X, p.Y, p.Z);

    /// <summary>
    /// All placement surfaces of the space: each entity's placement layouts (via
    /// its <see cref="PlacementLayoutSource"/> components — a floor slab's top
    /// face, a shelf board…).
    /// </summary>
    public IEnumerable<GridLayout<bool>> Surfaces => Entities
        .SelectMany(e => e.GetComponents<PlacementLayoutSource>())
        .Where(c => c.Layout is not null)
        .Select(c => c.Layout!);

    // ── Entity placement / queries ──────────────────────

    /// <summary>
    /// True if placing <paramref name="entity"/> at <paramref name="cs"/> would
    /// collide: the anchor or any of its (local) shape voxels lands on an
    /// occupied cell.
    /// </summary>
    public bool IsEntityCollided(Entity entity, Int3 cs)
    {
        if (Layout[cs] is not null)
            return true;

        foreach (var cell in entity.Volume.Cells3D())
        {
            if (Layout[cs + cell] is not null)
                return true;
        }
        return false;
    }

    /// <summary>
    /// True if placing <paramref name="src"/> at <paramref name="cs"/> intersects
    /// the specific <paramref name="dest"/> entity (dest voxels vs src voxels).
    /// </summary>
    public bool IsEntityCollided(Entity dest, Entity src, Int3 cs)
    {
        var destCells = dest.Volume.Cells3D()
            .Select(v => dest.Coords + v)
            .ToHashSet();
        return src.Volume.Cells3D().Any(v => destCells.Contains(cs + v));
    }

    /// <summary>
    /// Builds (places) the entity at <paramref name="cs"/>: writes EVERY one of
    /// its shape voxels into the grid and registers it. False if collided.
    /// </summary>
    public bool BuildAt(Entity entity, Int3 cs)
    {
        if (IsEntityCollided(entity, cs))
            return false;

        entity.Coords = cs;
        foreach (var cell in entity.Volume.Cells3D())
        {
            Layout[cs + cell] = entity;
        }
        Entities.Add(entity);
        return true;
    }

    /// <summary>
    /// Undoes <see cref="BuildAt"/> at the entity's registered position:
    /// removes the entity whose Coords equals <paramref name="pos"/>.
    /// </summary>
    public bool DestroyAt(Int3 pos)
    {
        var entity = Entities.FirstOrDefault(e => e.Coords == pos);
        return entity is not null && Remove(entity);
    }

    /// <summary>Removes the entity covering the given target cell.</summary>
    public bool DestroyTarget(Int3 target)
    {
        if (Layout[target] is not Entity entity)
            return false;
        return Remove(entity);
    }

    /// <summary>
    /// Clears the grid and re-rasterizes every held entity — a forced flush
    /// after direct low-level cell writes.
    /// </summary>
    public void Rebuild()
    {
        Layout.Clear();
        foreach (var entity in Entities)
        {
            foreach (var cell in entity.Volume.Cells3D())
            {
                Layout[entity.Coords + cell] = entity;
            }
        }
    }

    /// <summary>Finds an entity by its unique Id across this space.</summary>
    public Entity? FindEntity(Guid id) =>
        Entities.FirstOrDefault(e => e.Id == id);

    /// <summary>
    /// Returns all entities whose shape intersects the axis-aligned box
    /// <paramref name="min"/>–<paramref name="max"/> (inclusive). Drag-select.
    /// </summary>
    public List<Entity> GetEntitiesInBound(Int2 min, Int2 max)
    {
        var result = new List<Entity>();
        foreach (var entity in Entities)
        {
            foreach (var loc in entity.Volume.Cells3D())
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
    public List<TEntity> GetEntitiesOfType<TEntity>() where TEntity : Entity =>
        Entities.OfType<TEntity>().ToList();

    private bool Remove(Entity entity)
    {
        if (!Entities.Remove(entity))
            return false;

        foreach (var cell in entity.Volume.Cells3D())
        {
            var pos = entity.Coords + cell;
            if (Layout[pos] == entity)
                Layout[pos] = default!;
        }
        return true;
    }

    // ── IVoxelGeometry3D (upward composition) ────────────

    /// <inheritdoc/>
    public IEnumerable<Int3> Cells3D() =>
        Entities.SelectMany(e => e.Volume.Cells3D().Select(c => e.Coords + c));

    /// <inheritdoc/>
    public void PlaceAt(VoxelLayout<Entity> target, Int3 at)
    {
        foreach (var entity in Entities)
        {
            foreach (var cell in entity.Volume.Cells3D())
            {
                target[at + entity.Coords + cell] = entity;
            }
        }
    }

    /// <inheritdoc/>
    public void DestroyAt(VoxelLayout<Entity> target, Int3 at)
    {
        foreach (var entity in Entities)
        {
            foreach (var cell in entity.Volume.Cells3D())
            {
                var pos = at + entity.Coords + cell;
                if (target[pos] == entity)
                    target[pos] = default!;
            }
        }
    }
}
