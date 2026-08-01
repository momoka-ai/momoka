using Momoka.Home.Primitives;

namespace Momoka.Home.Models.Interfaces;

/// <summary>
/// Capability of a space to carry a 2D ground/floor surface made of tile
/// entities (lawn, paving, wooden flooring...).
/// </summary>
public interface IFloorCanvasSurface
{
    Canvas<TileEntity, Int2> FloorCanvas { get; }
}
