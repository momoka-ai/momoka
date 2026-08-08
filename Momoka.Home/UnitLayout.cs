using Momoka.Home.Components;
using Momoka.Home.Entities;
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
public sealed class UnitLayout : IEntitySource
{
    private VoxelLayout<Entity> _layout = new();

    /// <summary>The pure 3D voxel occupancy grid: the single root space.</summary>
    public VoxelLayout<Entity> Layout => _layout;

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

    /// <summary>
    /// Restores a loaded snapshot (save/load path): swaps in the reconstructed
    /// grid, repopulates the entity list and sets the region layer. Internal —
    /// the grid must come from the storage codecs so palette references stay
    /// consistent with <paramref name="entities"/>.
    /// </summary>
    internal void Restore(VoxelLayout<Entity> grid, IEnumerable<Entity> entities, ColumnLayout<Region>? regions)
    {
        _layout = grid;
        Entities.Clear();
        Entities.AddRange(entities);
        Regions = regions;
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
    public bool IsCollided(Entity entity, Int3 cs)
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
    public bool IsCollided(Entity dest, Entity src, Int3 cs)
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
    public bool PlaceAt(Entity entity, Int3 cs)
    {
        if (IsCollided(entity, cs))
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
    /// Removes the entity covering the given target cell (indexed by occupancy,
    /// not the placement anchor). False when the cell is empty.
    /// </summary>
    public bool DestroyAt(Int3 target)
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
}
