using Momoka.Home;
using Momoka.Home.Components;
using Momoka.Home.Entities;
using Momoka.Home.Layouts;
using Momoka.Home.Primitives;
namespace Momoka.Home.Levels;

/// <summary>
/// A floor of a building: an <see cref="Entity{Int3}"/> composed of a voxel
/// occupancy container (<see cref="Layout"/>), a boundary partition graph
/// (whose bounded faces are the rooms), floor/ceiling planes (placement
/// surfaces + material regions), and a region layout. Coordinates are local to
/// the owning building.
/// </summary>
public class Level : Entity<Int3>
{
    /// <summary>The voxel occupancy container backing this floor.</summary>
    public VoxelLayout3D Layout { get; } = new();

    /// <summary>
    /// Floor plane: placement surface (Direction = Up) + material subdivision.
    /// Size established by the operation logic when the level footprint is known.
    /// </summary>
    public PlaneLayout<Entity<Int2>> Floor { get; } = new(new Int2(50, 50)) { Direction = Int3.Up };

    /// <summary>
    /// Ceiling plane: attachment surface (Direction = Down, for hanging fixtures)
    /// + material subdivision.
    /// </summary>
    public PlaneLayout<Entity<Int2>> Ceiling { get; } = new(new Int2(50, 50)) { Direction = Int3.Down };

    /// <summary>Boundary partition graph (walls, fences…) with build/demolish.</summary>
    public FloorPlanLayout Plan { get; } = new();

    public GridLayout2D<Region> Regions { get; } = new(new Int2(50, 50));

    /// <summary>
    /// All placement surfaces of this level: the floor plane, the ceiling plane,
    /// and every contained entity's own surfaces (from its
    /// <see cref="SurfaceSource"/> component — walls' faces, shelves…). Placement
    /// logic queries this single catalog and uses each surface's
    /// <see cref="VoxelLayout2D.Direction"/> to orient objects.
    /// </summary>
    public IEnumerable<VoxelLayout2D> Layouts
    {
        get => new[] { Floor, Ceiling }
                .Concat(Layout.Entities.SelectMany(e => e.GetComponent<SurfaceSource>()?.Layouts ?? Enumerable.Empty<VoxelLayout2D>()));
    }
}
