using Momoka.Home.Primitives;
using Momoka.Home.Storage;
namespace Momoka.Home.Geometry;

/// <summary>
/// An arbitrary (convex or concave) polygon footprint in the local XZ plane,
/// rasterized by bounding-box sampling + even-odd point-in-polygon. The precise
/// footprint for irregular building shapes — no bounding-box overreach.
/// </summary>
[JsonTypeName("polygon")]
public class Polygon2D : Shape
{
    public List<Int2> Vertices { get; set; } = new();

    public Polygon2D() { }
    public Polygon2D(params Int2[] vertices) => Vertices.AddRange(vertices);
    public Polygon2D(IEnumerable<Int2> vertices) => Vertices.AddRange(vertices);

    public override IEnumerable<Int2> Cells2D()
    {
        if (Vertices.Count < 3)
            yield break;

        var minX = Vertices.Min(v => v.X);
        var maxX = Vertices.Max(v => v.X);
        var minZ = Vertices.Min(v => v.Z);
        var maxZ = Vertices.Max(v => v.Z);

        for (var x = minX; x <= maxX; x++)
        {
            for (var z = minZ; z <= maxZ; z++)
            {
                var cell = new Int2(x, z);
                if (ContainsCenter(cell))
                    yield return cell;
            }
        }
    }

    /// <summary>
    /// True if the cell's CENTER (x+0.5, z+0.5) lies inside the polygon — boundary
    /// cells are included in the footprint.
    /// </summary>
    public bool ContainsCenter(Int2 cell)
    {
        var px = cell.X + 0.5;
        var pz = cell.Z + 0.5;
        var inside = false;
        for (int i = 0, j = Vertices.Count - 1; i < Vertices.Count; j = i++)
        {
            var a = Vertices[i];
            var b = Vertices[j];
            if ((a.Z > pz) != (b.Z > pz) &&
                px < (b.X - a.X) * (pz - a.Z) / (double)(b.Z - a.Z) + a.X)
            {
                inside = !inside;
            }
        }
        return inside;
    }

    /// <summary>Even-odd point-in-polygon test (ray cast to +X).</summary>
    public bool Contains(Int2 point)
    {
        var inside = false;
        for (int i = 0, j = Vertices.Count - 1; i < Vertices.Count; j = i++)
        {
            var a = Vertices[i];
            var b = Vertices[j];
            if ((a.Z > point.Z) != (b.Z > point.Z) &&
                point.X < (b.X - a.X) * (point.Z - a.Z) / (double)(b.Z - a.Z) + a.X)
            {
                inside = !inside;
            }
        }
        return inside;
    }
}
