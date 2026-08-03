using Momoka.Home.Primitives;

namespace Momoka.Home;

/// <summary>
/// A floor of a building: a <see cref="VoxelGridEntity"/> composed of a wall
/// subdivision (whose bounded faces are the rooms), floor/ceiling planes
/// (placement surfaces + material regions), and a region layout. Coordinates
/// are local to the owning building (see <see cref="VoxelEntity.Coords"/>).
/// </summary>
public class Level : VoxelGridEntity, IVoxelLayout2DSource
{
    /// <summary>
    /// Floor plane: placement surface (Direction = Up) + material subdivision.
    /// Size established by the operation logic when the level footprint is known.
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
    /// and each wall's two faces. Placement logic queries this single catalog
    /// and uses each surface's <see cref="VoxelLayout2D.Direction"/> to orient
    /// objects.
    /// </summary>
    public IEnumerable<VoxelLayout2D> Layouts
    {
        get => new[] { Floor, Ceiling }
                .Concat(Entities.OfType<Wall>().SelectMany(x => x.Layouts));
    }

    /// <summary>
    /// Builds a wall segment from <paramref name="from"/> to <paramref name="to"/>:
    /// anchors the wall (its LineShape is local), registers the boundary edge,
    /// and rasterizes it into the occupancy grid.
    /// </summary>
    public bool BuildWall(Int2 from, Int2 to, Wall? wall = null)
    {
        wall ??= new Wall();
        var shape = (LineShape)wall.Shape;

        // Shape is local: anchor the wall at `from`, the segment is relative.
        wall.Coords = from.ToInt3();
        shape.Start = Float3.Zero;
        shape.End = (to - from).ToFloat3();

        Boundary.AddNode(from);
        Boundary.AddNode(to);
        Boundary.AddEdge(from, to, wall);

        foreach (var cell in shape.GetVoxels())
        {
            this[wall.Coords + cell] = wall;
        }

        Entities.Add(wall);
        return true;
    }
}
