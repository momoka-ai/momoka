using Momoka.Home.Models.Entities;
using Momoka.Home.Models.Layouts;
using Momoka.Home.Primitives;

namespace Momoka.Home.Models;

/// <summary>
/// The outermost digital-twin root: the whole property/residence.
///
/// Home is a <see cref="VoxelGridEntity"/> — the yard grid. Buildings,
/// fences, and terrain features are placed into this grid as ordinary
/// <see cref="VoxelEntity"/> instances, so external-view editing reuses the
/// same block-editing logic as interiors. Its grid height should be set to the
/// tallest building's height and its <see cref="VoxelGridEntity.Bound"/>
/// to the whole yard.
///
/// As an open-air space, Home composes a fence/boundary topology graph, a
/// ground surface, and a region layout — but deliberately has no ceiling canvas.
///
/// Accessory structures (garage, shed, pool house) live in
/// <see cref="Buildings"/>. Site-wide data sources (Weather, GPS, TimeZone,
/// Location) and whole-home command components are mounted as entity components.
/// </summary>
public class Home : VoxelGridEntity
{
    /// <summary>Fence/boundary topology across the yard; its bounded faces enclose lawn, driveway, pool areas.</summary>
    public Graph2D<VoxelEntity> Boundary { get; } = new();

    /// <summary>Ground surface (lawn, paving) as material regions.</summary>
    public Subdivision<TileEntity> Ground { get; } = new();

    /// <summary>Named outdoor areas (lawn, driveway, pool) on the property.</summary>
    public GridLayout2D<Region> Regions { get; } = new(new Int2(50, 50));

    /// <summary>Accessory structures attached to or standing on the property.</summary>
    public List<Building> Buildings { get; } = new();
}
