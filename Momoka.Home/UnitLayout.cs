using Momoka.Home.Components;
using Momoka.Home.Entities;
using Momoka.Home.Layouts;
using Momoka.Home.Primitives;
using Momoka.Home.Properties;
namespace Momoka.Home;

/// <summary>
/// A unit of living space: the fully-3D, multi-layer spatial root of a
/// residence — the final form of space. Everything — floor slabs, ceilings,
/// walls, furniture, yard objects — is an <see cref="Entity"/> placed in the
/// single root grid, so placement and collision run directly against one root
/// space with root-absolute coordinates (no nested offset chains). UnitLayout
/// owns the entity list and the placement operations; <see cref="Voxels"/> is
/// the pure cell grid underneath. Space semantics — rooms / walkable areas —
/// are the <see cref="Regions"/> layer (replacing the retired floor-plan
/// graphs).
/// </summary>
public sealed class UnitLayout : IEntitySource, IVoxelSource<Entity>
{
    public VoxelLayout<Entity> Voxels { get; set; }
    public VoxelLayout<Region> Regions { get; set; }
    public List<Entity> Entities { get; init; }

    public float VoxelSize { get; set; } = 10.0f;

    public UnitLayout()
    {
        Voxels = new();
        Regions = new();
        Entities = new();
    }

    public UnitLayout(VoxelLayout<Entity> voxelLayout, VoxelLayout<Region> regionLayout, List<Entity> entities)
    {
        Voxels = voxelLayout;
        Regions = regionLayout;
        Entities = entities;
    }

    public record class AtQuery(UnitLayout Source, Int3 Pos)
    {
        public Entity? Entity
        {
            get => Source.Voxels[Pos];
            set => Source.Voxels[Pos] = value;
        }

        public Region? Region
        {
            get => Source.Regions[Pos];
            set
            {
                var voxels = Source.Voxels;
                var regions = Source.Regions;
                if (voxels[Pos].IsImmutable())
                {
                    return;
                }

                var column = voxels.GetIteratorAt(Pos.X, Pos.Z);
                int? ceiling = column
                    .Where(c => c.Y > Pos.Y && c.Value.IsImmutable())
                    .Select(c => (int?)c.Y)
                    .FirstOrDefault();

                // 只考虑被上下结构范围夹住的列
                if (ceiling is null ||
                    !column.Any(c => c.Y < Pos.Y && c.Value.IsImmutable()))
                {
                    return;
                }

                for (var y = ceiling.Value - 1; y >= column.MinY && !voxels[new Int3(Pos.X, y, Pos.Z)].IsImmutable(); y--)
                {
                    regions[new Int3(Pos.X, y, Pos.Z)] = value;
                }
            }
        }
    }

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
    /// True if placing <paramref name="src"/> at <paramref name="position"/>
    /// (world units, cm) intersects the specific <paramref name="dest"/> entity
    /// (dest voxels vs src voxels).
    /// </summary>
    public bool IsCollided(Entity dest, Entity src, Float3 position)
    {
        var cs = Voxels.GetAsRelative(position);
        var destAnchor = Voxels.GetAsRelative(dest.Pos.Absolute());
        var destCells = dest.Volume.Cells3D()
            .Select(v => destAnchor + v)
            .ToHashSet();
        return src.Volume.Cells3D().Any(v => destCells.Contains(cs + v));
    }

    /// <summary>
    /// Builds (places) the entity at <paramref name="position"/> (world units,
    /// cm): rounds it to the anchor cell, writes EVERY one of its shape voxels
    /// into the grid and registers it. False if collided.
    /// </summary>
    public bool PlaceAt(Entity entity, Float3 position)
    {
        if (this.IsCollidedVolume(new Position(position), entity.Volume) is not null)
            return false;

        entity.Pos = new Position(position);
        var cs = Voxels.GetAsRelative(position);
        foreach (var cell in entity.Volume.Cells3D())
        {
            Voxels[cs + cell] = entity;
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
        if (Voxels[target] is not Entity entity)
            return false;
        return Remove(entity);
    }

    /// <summary>
    /// Clears the grid and re-rasterizes every held entity — a forced flush
    /// after direct low-level cell writes.
    /// </summary>
    public void Rebuild()
    {
        Voxels.Clear();
        foreach (var entity in Entities)
        {
            var cs = Voxels.GetAsRelative(entity.Pos.Absolute());
            foreach (var cell in entity.Volume.Cells3D())
            {
                Voxels[cs + cell] = entity;
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
            var cs = Voxels.GetAsRelative(entity.Pos.Absolute());
            foreach (var loc in entity.Volume.Cells3D())
            {
                var p = cs + loc;
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

        var cs = Voxels.GetAsRelative(entity.Pos.Absolute());
        foreach (var cell in entity.Volume.Cells3D())
        {
            var pos = cs + cell;
            if (Voxels[pos] == entity)
                Voxels[pos] = default!;
        }
        return true;
    }
}
