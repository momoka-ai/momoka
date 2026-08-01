using Momoka.Home.Primitives;

namespace Momoka.Home.Models.Interfaces;

/// <summary>
/// Capability of a space to carry a 2D ceiling surface. Only enclosed spaces
/// (rooms, underground) have ceilings; outdoor spaces do not.
/// </summary>
public interface ICeilingCanvasSurface
{
    Canvas<TileEntity, Int2> CeilingCanvas { get; }
}
