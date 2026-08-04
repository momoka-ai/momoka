using Momoka.Home.Components;
using Momoka.Home.Entities;
using Momoka.Home.Layouts;
using Momoka.Home.Primitives;
namespace Momoka.Home;

/// <summary>
/// The outermost digital-twin root: the whole property/residence.
///
/// Home is a spatial container (<see cref="IEntitySource"/>) — the yard. Its
/// voxel occupancy container (<see cref="Layout"/>) holds buildings, fences and
/// terrain features as ordinary entities; its grid height should be set to the
/// tallest building's height and its <see cref="VoxelLayout3D.Bound"/> to the
/// whole yard.
///
/// As an open-air space, Home composes a fence/boundary topology graph, a ground
/// surface and a region layout — deliberately no ceiling canvas. Site-wide data
/// sources and whole-home command components are mounted via
/// <see cref="IComponentSource"/>. Hand-built, not config-driven.
/// </summary>
public class Home : IEntitySource, IComponentSource
{
    public VoxelLayout3D Layout { get; } = new();
    public FloorPlanLayout Boundary { get; } = new();
    public Subdivision<Entity<Int2>> Ground { get; } = new();
    public GridLayout2D<Region> Regions { get; } = new(new Int2(50, 50));
    public List<Building> Buildings { get; } = new();

    /// <summary>All entities in the yard: layout content plus accessory buildings.</summary>
    public IReadOnlyList<Entity> Entities =>
        Layout.Entities.Concat(Buildings).ToList();

    public IList<Component> Components => _components;
    private readonly List<Component> _components = new();
}
