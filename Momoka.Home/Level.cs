using Momoka.Home;
using Momoka.Home.Components;
using Momoka.Home.Entities;
using Momoka.Home.Geometry;
using Momoka.Home.Layouts;
using Momoka.Home.Primitives;
namespace Momoka.Home;

/// <summary>
/// A floor of a building: a spatial container (<see cref="IEntitySource"/>) of a
/// voxel occupancy (<see cref="Layout"/>), a boundary partition graph (whose
/// bounded faces are the rooms), floor/ceiling planes (placement surfaces +
/// material regions). Hand-built, not config-driven;
/// coordinates are local to the owning building.
/// </summary>
public class Level : IEntitySource, IVoxelGeometry3D
{
    /// <summary>Position of this floor within the owning building.</summary>
    public Int3 Coords { get; set; }

    /// <summary>The voxel occupancy container backing this floor.</summary>
    public VoxelLayout<Entity<Int3>> Layout { get; } = new();

    public PlaneLayout<Entity<Int2>> Floor { get; } = new(new Int2(50, 50)) { Direction = Int3.Up };
    public PlaneLayout<Entity<Int2>> Ceiling { get; } = new(new Int2(50, 50)) { Direction = Int3.Down };
    public FloorPlanLayout Plan { get; } = new();

    /// <inheritdoc/>
    public IReadOnlyList<Entity> Entities => Layout.Entities;

    /// <summary>
    /// All placement surfaces of this level: the floor plane, the ceiling plane,
    /// the floor plan's derived partition surfaces (walls' faces, computed on
    /// demand from the graph), and every contained entity's own surfaces (from its
    /// <see cref="VoxelLayoutSource"/> component — shelves…).
    /// </summary>
    public IEnumerable<GridLayout<bool>> Layouts
    {
        get => new[] { Floor, Ceiling }
                .Concat(Plan.Surfaces)
                .Concat(Layout.Entities.SelectMany(e => e.GetComponent<VoxelLayoutSource>()?.Layouts ?? Enumerable.Empty<GridLayout<bool>>()));
    }

    /// <inheritdoc/>
    public IEnumerable<Int3> Cells3D() =>
        Layout.Entities.SelectMany(e => e.Volume.Cells3D().Select(c => e.Coords + c));

    /// <inheritdoc/>
    public void PlaceAt(VoxelLayout<Entity<Int3>> target, Int3 at) => target.MergeFrom(Layout, at);

    /// <inheritdoc/>
    public void DestroyAt(VoxelLayout<Entity<Int3>> target, Int3 at) => target.RemoveFrom(Layout, at);
}
