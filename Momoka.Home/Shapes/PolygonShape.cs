using Momoka.Home.Primitives;
namespace Momoka.Home.Shapes;

/// <summary>Polygonal prism: arbitrary polygon footprint × height (irregular buildings).</summary>
public class PolygonShape : ExtrudedShape
{
    public PolygonShape(IEnumerable<Int2> vertices, int height)
        : base(new Polygon2D(vertices), height)
    {
    }
}
