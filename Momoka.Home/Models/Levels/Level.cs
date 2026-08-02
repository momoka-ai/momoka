using Momoka.Home.Models.Entities;
using Momoka.Home.Models.Layouts;
using Momoka.Home.Primitives;

namespace Momoka.Home.Models.Levels;

/// <summary>
/// A floor of a building: a <see cref="BlockGridEntity"/> composed of a
/// wall topology graph, floor/ceiling surface canvases, and a region layout.
/// Coordinates are local to the owning building (see
/// <see cref="BlockEntity.Coords"/>).
/// </summary>
public class Level : BlockGridEntity
{
    public Canvas<TileEntity, Int2> FloorCanvas { get; } = new();
    public Canvas<TileEntity, Int2> CeilingCanvas { get; } = new();
    public Graph2D<BlockEntity> Boundary { get; } = new();
    public GridLayout2D<Region> Regions { get; } = new(new Int2(50, 50));
}
