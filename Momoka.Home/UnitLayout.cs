using Momoka.Home.Components;
using Momoka.Home.Entities;
using Momoka.Home.Geometry;
using Momoka.Home.Layouts;
using Momoka.Home.Primitives;
namespace Momoka.Home;

/// <summary>
/// A unit of living space: the fully-3D, multi-layer spatial root of a
/// residence — the final form of space. Everything — floor slabs, ceilings,
/// walls, furniture, yard objects — is an <see cref="Entity"/> inside the single
/// <see cref="VoxelLayout{T}"/>, so placement and collision run directly against
/// one root space with root-absolute coordinates (no nested offset chains).
/// Space semantics — rooms / walkable areas — are the <see cref="Regions"/>
/// layer (replacing the retired floor-plan graphs).
/// </summary>
public sealed class UnitLayout : IEntitySource, IVoxelGeometry3D
{
    /// <summary>The 3D voxel occupancy container: the single root space.</summary>
    public VoxelLayout<Entity> Layout { get; } = new();

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
        Regions = Region.BuildLayout(Layout, agent);
        return Regions;
    }

    /// <summary>The region containing the cell, or null (blocked / outside / unbuilt).</summary>
    public Region? RegionAt(Int3 p) => Regions?.At(p.X, p.Y, p.Z);

    /// <inheritdoc/>
    public IReadOnlyList<Entity> Entities => Layout.Entities;

    /// <summary>
    /// All placement surfaces of the space: each entity's placement layouts (via
    /// its <see cref="PlacementLayoutSource"/> components — a floor slab's top
    /// face, a shelf board…).
    /// </summary>
    public IEnumerable<GridLayout<bool>> Surfaces => Layout.Entities
        .SelectMany(e => e.GetComponents<PlacementLayoutSource>())
        .Where(c => c.Layout is not null)
        .Select(c => c.Layout!);

    /// <inheritdoc/>
    public IEnumerable<Int3> Cells3D() =>
        Layout.Entities.SelectMany(e => e.Volume.Cells3D().Select(c => e.Coords + c));

    /// <inheritdoc/>
    public void PlaceAt(VoxelLayout<Entity> target, Int3 at) => target.MergeFrom(Layout, at);

    /// <inheritdoc/>
    public void DestroyAt(VoxelLayout<Entity> target, Int3 at) => target.RemoveFrom(Layout, at);
}
