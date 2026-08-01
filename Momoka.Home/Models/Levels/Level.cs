using Momoka.Home.Models.Entities;
using Momoka.Home.Models.Interfaces;
using Momoka.Home.Primitives;

namespace Momoka.Home.Models.Levels;

/// <summary>
/// A floor of a building: a <see cref="BlockCompositionEntity"/> with a wall
/// topology graph, floor/ceiling surface canvases, and named regions.
/// Coordinates are local to the owning building (see
/// <see cref="BlockEntity.Coords"/>).
/// </summary>
public class Level : BlockCompositionEntity,
    IWallGraph,
    IFloorCanvasSurface,
    ICeilingCanvasSurface,
    IRegionLayout
{
    public Canvas<TileEntity, Int2> FloorCanvas { get; } = new();
    public Canvas<TileEntity, Int2> CeilingCanvas { get; } = new();
    public Graph2D<BlockEntity> WallGraph { get; } = new();
    public List<Region> Regions { get; } = new();
}
