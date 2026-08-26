using Momoka.Home.Primitives;
namespace Momoka.Home.Levels;

/// <summary>
/// A 3D region — a connected component of standable space (a room, a walkable
/// area). Region ids are assigned by whoever builds the layer (0 = unassigned)
/// and are not stable across rebuilds. The value type stored in
/// <see cref="LevelLayout.Regions"/> (a <c>VoxelLayout&lt;Region&gt;</c>);
/// per-id names are persisted in the <c>RegionNames</c> store table.
/// </summary>
public sealed class Region
{
    /// <summary>1-based region id in the owning region layer.</summary>
    public int Id { get; }

    /// <summary>Inclusive axis-aligned bounds of all cells in the region.</summary>
    public Bound Bounds { get; }

    /// <summary>Total cells (each cell is 10 cm × 10 cm × 10 cm).</summary>
    public long Volume { get; }

    /// <summary>Distinct (x, z) columns in the region's footprint.</summary>
    public int Area { get; }

    /// <summary>Human-readable label to tell spaces apart (e.g. "Bedroom"); set by the caller.</summary>
    public string Name { get; set; }

    internal Region(int id, Bound bounds, long volume, int area)
    {
        Id = id;
        Bounds = bounds;
        Volume = volume;
        Area = area;
        Name = $"Region {id}";
    }
}
