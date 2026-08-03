using Momoka.Home.Models.Entities;
using Momoka.Home.Models.Layouts;
using Momoka.Home.Primitives;

namespace Momoka.Home.Models.Levels;

/// <summary>
/// A floor of a building: a <see cref="VoxelGridEntity"/> composed of a wall
/// subdivision (whose bounded faces are the rooms), floor/ceiling surface
/// canvases, and a region layout. Coordinates are local to the owning building
/// (see <see cref="VoxelEntity.Coords"/>).
/// </summary>
public class Level : VoxelGridEntity
{
    public Canvas<TileEntity, Int2> FloorCanvas { get; } = new();
    public Canvas<TileEntity, Int2> CeilingCanvas { get; } = new();
    public Subdivision<TileEntity> Ground { get; } = new();
    public Graph2D<VoxelEntity> Boundary { get; } = new();
    public GridLayout2D<Region> Regions { get; } = new(new Int2(50, 50));
}
