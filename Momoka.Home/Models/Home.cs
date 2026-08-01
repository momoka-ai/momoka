using Momoka.Home.Models.Buildings;
using Momoka.Home.Models.Entities;
using Momoka.Home.Models.Interfaces;
using Momoka.Home.Primitives;

namespace Momoka.Home.Models;

/// <summary>
/// The outermost digital-twin root: the whole property/residence.
///
/// Home is a <see cref="BlockCompositionEntity"/> — the yard grid. Buildings,
/// fences, and terrain features are placed into this grid as ordinary
/// <see cref="BlockEntity"/> instances, so external-view editing reuses the
/// same block-editing logic as interiors. Its grid height should be set to the
/// tallest building's height and its <see cref="BlockCompositionEntity.Bound"/>
/// to the whole yard.
///
/// As an open-air space, Home carries a fence/wall topology graph and a ground
/// surface but deliberately has no ceiling (it does not implement
/// <see cref="ICeilingCanvasSurface"/>).
///
/// Accessory structures (garage, shed, pool house) live in
/// <see cref="Buildings"/>. Site-wide data sources (Weather, GPS, TimeZone,
/// Location) and whole-home command components are mounted as entity components.
/// </summary>
public class Home : BlockCompositionEntity, IWallGraph, IFloorCanvasSurface, IRegionLayout
{
    /// <summary>Fence and boundary topology across the yard.</summary>
    public Graph2D<BlockEntity> WallGraph { get; } = new();

    /// <summary>Ground surface (lawn, paving) as 2D tiles.</summary>
    public Canvas<TileEntity, Int2> FloorCanvas { get; } = new();

    /// <summary>Named outdoor areas (lawn, driveway, pool) on the property.</summary>
    public List<Region> Regions { get; } = new();

    /// <summary>Accessory structures attached to or standing on the property.</summary>
    public List<Building> Buildings { get; } = new();
}
