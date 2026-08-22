using Momoka.Home.Primitives;
using Momoka.Home.Data.Json;
namespace Momoka.Home.Geometry;

/// <summary>多边形棱柱：任意多边形截面 × 高度（异形建筑）。</summary>
[JsonTypeName("polygon")]
public class Polygon3D : Extruded3D
{
    public Polygon3D() : base(Array.Empty<Int2>(), 1) { }

    public Polygon3D(IEnumerable<Int2> vertices, int height)
        : base(Rasterizer.FilledPolygon(vertices), height)
    {
    }
}
