using Momoka.Home;
using Momoka.Home.Geometry;
using Momoka.Home.Layouts;
using Momoka.Home.Primitives;
namespace Momoka.Home.Entities;

/// <summary>
/// A physically bounded structure with floors (<see cref="Levels"/>).
/// Every enclosed habitable or usable volume — the main house, a garage,
/// a shed, a pool house — is a <see cref="Building"/>.
///
/// In external view a building is a complete <see cref="Entity{Int3}"/>: its
/// footprint (<see cref="Bound"/>) is exposed through its <see cref="Shape"/>
/// so it can be placed directly into a parent composition (e.g. the yard grid).
/// Its interior is modeled as <see cref="Levels"/>, each in building-local
/// coordinates; moving the building only updates its position in the parent grid,
/// never its interior blocks.
/// </summary>
public class Building : Entity<Int3>, IVoxelGeometry3D
{
    /// <summary>Inclusive 3D footprint of this building in its parent space.</summary>
    public Bound Bound { get; set; } = Bound.Empty;

    /// <summary>Floors of this building, in building-local coordinates.</summary>
    public Dictionary<int, Level> Levels { get; } = new();

    public Building()
    {
        Volume = new Box3D { SizeX = Bound.SizeX, SizeZ = Bound.SizeZ };
    }

    /// <summary>
    /// Sets the building's footprint, positions it in the parent space, and
    /// updates its exterior <see cref="Volume"/> accordingly.
    /// </summary>
    public void SetFootprint(Bound bound)
    {
        Bound = bound;
        Coords = bound.Min;
        Volume = new Box3D { SizeX = bound.SizeX, SizeZ = bound.SizeZ };
    }

    /// <summary>
    /// The building's full voxel view: all floors' occupancy merged into
    /// building-local space (level coords + level-local cells). Derived on demand.
    /// </summary>
    public VoxelLayout<Entity<Int3>> Layout
    {
        get
        {
            var layout = new VoxelLayout<Entity<Int3>>();
            foreach (var level in Levels.Values)
                layout.MergeFrom(level.Layout, level.Coords);
            return layout;
        }
    }

    /// <inheritdoc/>
    public IEnumerable<Int3> Cells3D() =>
        Levels.Values.SelectMany(level => level.Cells3D().Select(c => level.Coords + c));

    /// <inheritdoc/>
    public void PlaceAt(VoxelLayout<Entity<Int3>> target, Int3 at)
    {
        foreach (var level in Levels.Values)
            target.MergeFrom(level.Layout, at + level.Coords);
    }

    /// <inheritdoc/>
    public void DestroyAt(VoxelLayout<Entity<Int3>> target, Int3 at)
    {
        foreach (var level in Levels.Values)
            target.RemoveFrom(level.Layout, at + level.Coords);
    }
}
