using Momoka.Home.Primitives;
using Momoka.Home.Serialization;
namespace Momoka.Home.Geometry;

/// <summary>Circular prism (round towers, planters, fountains).</summary>
[JsonTypeName("circle")]
public class Circle3D : Extruded3D
{
    public Circle3D(int radius, int height) : base(new Circle2D(radius), height) { }
}

/// <summary>Elliptical prism.</summary>
[JsonTypeName("ellipse")]
public class Ellipse3D : Extruded3D
{
    public Ellipse3D(int radiusX, int radiusZ, int height) : base(new Ellipse2D(radiusX, radiusZ), height) { }
}

/// <summary>Annular prism (circular corridors, colonnades, pools).</summary>
[JsonTypeName("ring")]
public class Ring3D : Extruded3D
{
    public Ring3D(int innerRadius, int outerRadius, int height) : base(new Ring2D(innerRadius, outerRadius), height) { }
}

/// <summary>Vertical cylinder — a named alias of <see cref="Circle3D"/>.</summary>
[JsonTypeName("cylinder")]
public class Cylinder3D : Circle3D
{
    public Cylinder3D(int radius, int height) : base(radius, height) { }
}

/// <summary>Triangular prism (wedges, chamfered corners).</summary>
[JsonTypeName("triangle")]
public class Triangle3D : Polygon3D
{
    public Triangle3D(Int2 a, Int2 b, Int2 c, int height) : base(new[] { a, b, c }, height) { }
}
