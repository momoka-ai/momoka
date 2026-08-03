using Momoka.Home.Models.Entities;
using Momoka.Home.Primitives;
using Momoka.Home.Models.Shapes;

namespace Momoka.Home.Models;

public class Region
{
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Vertices of the closed polygon boundary, in counter-clockwise order.
    /// First and last points are not repeated — the edge from last to first is implicit.
    /// Must have at least 3 vertices.
    /// </summary>
    public List<Int2> Boundary { get; set; } = new();

    public bool Contains(Int2 point) => RayCasting(point, Boundary);

    public bool Contains(VoxelEntity entity)
    {
        foreach (var pos in entity.Shape.GetVoxels())
        {
            if (!Contains(pos.Xz))
                return false;
        }
        return true;
    }

    public float ContainmentRatio(VoxelEntity entity)
    {
        var total = 0;
        var inside = 0;
        foreach (var pos in entity.Shape.GetVoxels())
        {
            total++;
            if (Contains(pos.Xz))
                inside++;
        }
        return total == 0 ? 0f : (float)inside / total;
    }

    // ── Ray casting ───────────────────────────────────────

    /// <summary>
    /// Point-in-polygon via ray casting.
    /// Casts a horizontal ray to +X and counts edge crossings.
    /// Odd count = inside, even = outside.
    /// </summary>
    private static bool RayCasting(Int2 point, List<Int2> polygon)
    {
        var inside = false;
        for (int i = 0, j = polygon.Count - 1; i < polygon.Count; j = i++)
        {
            var a = polygon[i];
            var b = polygon[j];

            // Does the edge straddle the horizontal ray?
            if ((a.Z > point.Z) != (b.Z > point.Z))
            {
                // Compute X intersection of edge with ray
                var intersectX = (b.X - a.X) * (point.Z - a.Z) / (b.Z - a.Z) + a.X;
                if (point.X < intersectX)
                    inside = !inside;
            }
        }
        return inside;
    }
}