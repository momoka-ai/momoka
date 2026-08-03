using Momoka.Home.Models.Entities;
using Momoka.Home.Models.Layouts;
using Momoka.Home.Primitives;

namespace Momoka.Home.Models.Levels;

/// <summary>
/// A floor of a building: a <see cref="VoxelGridEntity"/> composed of a wall
/// subdivision (whose bounded faces are the rooms), floor/ceiling planes
/// (placement surfaces + material regions), and a region layout. Coordinates
/// are local to the owning building (see <see cref="VoxelEntity.Coords"/>).
/// </summary>
public class Level : VoxelGridEntity, IVoxelLayout2DSource
{
    /// <summary>
    /// Floor plane: placement surface (Direction = Up) + material subdivision +
    /// attachment layers (raised platforms). Size established by the operation
    /// logic when the level footprint is known.
    /// </summary>
    public PlaneLayout<TileEntity> Floor { get; } = new(new Int2(50, 50)) { Direction = Int3.Up };

    /// <summary>
    /// Ceiling plane: attachment surface (Direction = Down, for hanging fixtures)
    /// + material subdivision.
    /// </summary>
    public PlaneLayout<TileEntity> Ceiling { get; } = new(new Int2(50, 50)) { Direction = Int3.Down };

    public Graph2D<VoxelEntity> Boundary { get; } = new();
    public GridLayout2D<Region> Regions { get; } = new(new Int2(50, 50));

    /// <summary>
    /// All placement surfaces of this level: the floor plane, the ceiling plane,
    /// their attachment layers, and each wall's two faces. Placement logic
    /// queries this single catalog and uses each surface's
    /// <see cref="VoxelLayout2D.Direction"/> to orient objects.
    /// </summary>
    public IEnumerable<VoxelLayout2D> Layouts
    {
        get => Entities.OfType<Wall>()
                .SelectMany(x => x.Layouts)
                .Concat(Floor.Layouts)
                .Concat(Ceiling.Layouts);
    }
}
