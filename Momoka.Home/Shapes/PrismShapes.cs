using Momoka.Home.Primitives;
namespace Momoka.Home.Shapes;

/// <summary>Circular prism (round towers, planters, fountains).</summary>
public class CircleShape : ExtrudedShape
{
    public CircleShape(int radius, int height) : base(new Circle2D(radius), height) { }
}

/// <summary>Elliptical prism.</summary>
public class EllipseShape : ExtrudedShape
{
    public EllipseShape(int radiusX, int radiusZ, int height) : base(new Ellipse2D(radiusX, radiusZ), height) { }
}

/// <summary>Annular prism (circular corridors, colonnades, pools).</summary>
public class RingShape : ExtrudedShape
{
    public RingShape(int innerRadius, int outerRadius, int height) : base(new Ring2D(innerRadius, outerRadius), height) { }
}

/// <summary>Vertical cylinder — a named alias of <see cref="CircleShape"/>.</summary>
public class CylinderShape : CircleShape
{
    public CylinderShape(int radius, int height) : base(radius, height) { }
}

/// <summary>Triangular prism (wedges, chamfered corners).</summary>
public class TriangleShape : PolygonShape
{
    public TriangleShape(Int2 a, Int2 b, Int2 c, int height) : base(new[] { a, b, c }, height) { }
}
