using Momoka.Home.Components;
using Momoka.Home.Entities;
using Momoka.Home.Geometry;
using Momoka.Home.Primitives;
namespace Momoka.Home.Layouts;

/// <summary>
/// A unit of living space: the fully-3D, multi-layer spatial root of a
/// residence. Everything — floor slabs, ceilings, walls, furniture, yard
/// objects — is an <see cref="Entity{Int3}"/> inside the single
/// <see cref="VoxelLayout3D"/>, so placement and collision run directly against
/// one root space with root-absolute coordinates (no nested offset chains).
///
/// Floor plans are single-layer, so one <see cref="FloorPlanLayout"/> per layer
/// lives in <see cref="Floors"/> (list index = layer order, low to high). The
/// per-layer height is not stored here: the floor slab entities in
/// <see cref="Layout"/> carry it, and the plan graphs use their own partition
/// coordinates.
/// </summary>
public class UnitLayout : IEntitySource, IVoxelGeometry3D
{
    /// <summary>The 3D voxel occupancy container: the single root space.</summary>
    public VoxelLayout3D Layout { get; } = new();

    /// <summary>One floor plan per layer, low to high (list index = layer order).</summary>
    public List<FloorPlanLayout> Floors { get; } = new();

    /// <inheritdoc/>
    public IReadOnlyList<Entity> Entities => Layout.Entities;

    /// <summary>
    /// All placement surfaces of the space: every floor plan's partition faces
    /// plus each entity's own surfaces (via its <see cref="VoxelLayoutSource"/>
    /// component — a floor slab's top face, a shelf board…).
    /// </summary>
    public IEnumerable<VoxelLayout2D> Surfaces => Floors
        .SelectMany(f => f.Surfaces)
        .Concat(Layout.Entities.SelectMany(e =>
            e.GetComponent<VoxelLayoutSource>()?.Layouts ?? Enumerable.Empty<VoxelLayout2D>()));

    /// <inheritdoc/>
    public IEnumerable<Int3> Cells3D() =>
        Layout.Entities.SelectMany(e => e.Volume.Cells3D().Select(c => e.Coords + c));

    /// <inheritdoc/>
    public void PlaceAt(VoxelLayout3D target, Int3 at) => target.MergeFrom(Layout, at);

    /// <inheritdoc/>
    public void DestroyAt(VoxelLayout3D target, Int3 at) => target.RemoveFrom(Layout, at);
}
