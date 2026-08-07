using Momoka.Home.Components;
using Momoka.Home.Entities;
using Momoka.Home.Geometry;
using Momoka.Home.Layouts;
using Momoka.Home.Primitives;
using Momoka.Home.Regions;
namespace Momoka.Home;

/// <summary>
/// A unit of living space: the fully-3D, multi-layer spatial root of a
/// residence — the final form of space. Everything — floor slabs, ceilings,
/// walls, furniture, yard objects — is an <see cref="Entity"/> inside the single
/// <see cref="VoxelLayout{T}"/>, so placement and collision run directly against
/// one root space with root-absolute coordinates (no nested offset chains).
///
/// Floor plans are single-layer, so one <see cref="FloorPlanLayout"/> per layer
/// lives in <see cref="Floors"/> (list index = layer order, low to high). The
/// per-layer height is not stored here: the floor slab entities in
/// <see cref="Layout"/> carry it, and the plan graphs use their own partition
/// coordinates.
/// </summary>
public sealed class UnitLayout : IEntitySource, IVoxelGeometry3D
{
    /// <summary>The 3D voxel occupancy container: the single root space.</summary>
    public VoxelLayout<Entity> Layout { get; } = new();

    /// <summary>One floor plan per layer, low to high (list index = layer order).</summary>
    public List<FloorPlanLayout> Floors { get; } = new();

    /// <summary>
    /// The 3D region layer (rooms / walkable areas) of the space, built on
    /// demand — the successor of <see cref="Floors"/> as the space-semantics
    /// layer. Null until <see cref="RebuildRegions"/> has run.
    /// </summary>
    public RegionMap? Regions { get; private set; }

    /// <summary>
    /// (Re)builds the region layer from the current occupancy. Manual — call
    /// once at model ingestion; furniture placement/removal does not
    /// invalidate it. Structural edits should trigger a full scene rebuild.
    /// </summary>
    public RegionMap RebuildRegions(RegionRules? rules = null)
    {
        Regions = RegionMap.Build(Layout, rules);
        return Regions;
    }

    /// <inheritdoc/>
    public IReadOnlyList<Entity> Entities => Layout.Entities;

    /// <summary>
    /// All placement surfaces of the space: every floor plan's partition faces
    /// plus each entity's own surfaces (via its <see cref="VoxelLayoutSource"/>
    /// component — a floor slab's top face, a shelf board…).
    /// </summary>
    public IEnumerable<GridLayout<bool>> Surfaces => Floors
        .SelectMany(f => f.Surfaces)
        .Concat(Layout.Entities.SelectMany(e =>
            e.GetComponent<VoxelLayoutSource>()?.Layouts ?? Enumerable.Empty<GridLayout<bool>>()));

    /// <inheritdoc/>
    public IEnumerable<Int3> Cells3D() =>
        Layout.Entities.SelectMany(e => e.Volume.Cells3D().Select(c => e.Coords + c));

    /// <inheritdoc/>
    public void PlaceAt(VoxelLayout<Entity> target, Int3 at) => target.MergeFrom(Layout, at);

    /// <inheritdoc/>
    public void DestroyAt(VoxelLayout<Entity> target, Int3 at) => target.RemoveFrom(Layout, at);
}
