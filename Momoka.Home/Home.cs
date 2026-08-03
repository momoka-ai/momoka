using Momoka.Home.Entities;
using Momoka.Home.Layouts;
using Momoka.Home.Primitives;
namespace Momoka.Home;

/// <summary>
/// The outermost digital-twin root: the whole property/residence.
///
/// Home is a <see cref="VoxelEntity"/> — the yard. Its voxel occupancy
/// container (<see cref="Layout"/>) holds buildings, fences, and terrain
/// features placed as ordinary <see cref="VoxelEntity"/> instances, so
/// external-view editing reuses the same block-editing logic as interiors.
/// Its grid height should be set to the tallest building's height and its
/// <see cref="VoxelLayout3D.Bound"/> to the whole yard.
///
/// As an open-air space, Home composes a fence/boundary topology graph, a
/// ground surface, and a region layout — but deliberately has no ceiling canvas.
///
/// Accessory structures (garage, shed, pool house) live in
/// <see cref="Buildings"/>. Site-wide data sources (Weather, GPS, TimeZone,
/// Location) and whole-home command components are mounted as entity components.
/// </summary>
public class Home : VoxelEntity
{
    /// <summary>The voxel occupancy container backing the yard.</summary>
    public VoxelLayout3D Layout { get; } = new();

    /// <summary>Fence/boundary partition graph across the yard (fences, walls…); its bounded faces enclose lawn, driveway, pool areas.</summary>
    public GraphLayout2D Boundary { get; }

    /// <summary>Ground surface (lawn, paving) as material regions.</summary>
    public Subdivision<TileEntity> Ground { get; } = new();

    /// <summary>Named outdoor areas (lawn, driveway, pool) on the property.</summary>
    public GridLayout2D<Region> Regions { get; } = new(new Int2(50, 50));

    /// <summary>Accessory structures attached to or standing on the property.</summary>
    public List<Building> Buildings { get; } = new();

    public Home() => Boundary = new GraphLayout2D();
}
