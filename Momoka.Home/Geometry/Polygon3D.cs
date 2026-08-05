using Momoka.Home.Primitives;
using Momoka.Home.Serialization;
namespace Momoka.Home.Geometry;

/// <summary>Polygonal prism: arbitrary polygon footprint × height (irregular buildings).</summary>
[JsonTypeName("polygon")]
public class Polygon3D : Extruded3D
{
    public Polygon3D() : base(new Polygon2D(), 1) { }

    public Polygon3D(IEnumerable<Int2> vertices, int height)
        : base(new Polygon2D(vertices), height)
    {
    }
}
