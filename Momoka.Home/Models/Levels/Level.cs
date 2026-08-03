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
public class Level : VoxelGridEntity, IVoxelLayout2DSource
{
    /// <summary>Floor material regions (Subdivision of the ground plane).</summary>
    public Subdivision<TileEntity> Ground { get; } = new();

    /// <summary>Ceiling material regions.</summary>
    public Subdivision<TileEntity> Ceiling { get; } = new();

    public Graph2D<VoxelEntity> Boundary { get; } = new();
    public GridLayout2D<Region> Regions { get; } = new(new Int2(50, 50));

    /// <summary>
    /// Floor placement surface (Direction = Up). Its size is established by the
    /// operation logic when the level footprint is known.
    /// </summary>
    public VoxelLayout2D FloorSurface { get; set; } = new(new Int2(50, 50)) { Direction = Int3.Up };

    /// <summary>
    /// Ceiling placement surface (Direction = Down), for hanging fixtures.
    /// </summary>
    public VoxelLayout2D CeilingSurface { get; set; } = new(new Int2(50, 50)) { Direction = Int3.Down };

    /// <summary>
    /// All placement surfaces of this level: the floor, the ceiling, and each
    /// wall's two faces. Placement logic queries this single catalog and uses
    /// each surface's <see cref="VoxelLayout2D.Direction"/> to orient objects.
    /// </summary>
    public IReadOnlyList<VoxelLayout2D> Layouts
    {
        get
        {
            var layouts = new List<VoxelLayout2D> { FloorSurface, CeilingSurface };
            foreach (var wall in Entities.OfType<Wall>())
            {
                layouts.AddRange(wall.Layouts);
            }
            return layouts;
        }
    }
}
