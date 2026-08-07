using Momoka.Home.Primitives;
namespace Momoka.Home.Regions;

/// <summary>
/// A 3D region — a connected component of free space (a room, a walkable area),
/// aggregated from the column spans of a <see cref="RegionMap"/>. Region ids are
/// assigned per build (0 = unassigned) and are not stable across rebuilds.
/// </summary>
public sealed class Region
{
    /// <summary>1-based region id in the owning <see cref="RegionMap"/>.</summary>
    public int Id { get; }

    /// <summary>Inclusive axis-aligned bounds of all free cells in the region.</summary>
    public Bound Bounds { get; }

    /// <summary>Total free cells (each cell is 10 cm × 10 cm × 10 cm).</summary>
    public long Volume { get; }

    /// <summary>Distinct (x, z) columns in the region's footprint.</summary>
    public int Area { get; }

    internal Region(int id, Bound bounds, long volume, int area)
    {
        Id = id;
        Bounds = bounds;
        Volume = volume;
        Area = area;
    }
}
