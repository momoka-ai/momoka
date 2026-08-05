using Momoka.Home.Primitives;
namespace Momoka.Home.Geometry;

/// <summary>Polygonal prism: arbitrary polygon footprint × height (irregular buildings).</summary>
public class Polygon3D : Extruded3D
{
    public Polygon3D(IEnumerable<Int2> vertices, int height)
        : base(new Polygon2D(vertices), height)
    {
    }
}
