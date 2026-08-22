using Momoka.Home.Primitives;
using Momoka.Home.Data.Json;
namespace Momoka.Home.Levels.Volumes;

/// <summary>圆柱（圆塔、花池、喷泉）：圆形截面挤出。</summary>
[JsonTypeName("circle")]
public class Circle : Extruded
{
    public Circle() : base(Rasterizer.FilledCircle(1), 1) { }
    public Circle(int radius, int height) : base(Rasterizer.FilledCircle(radius), height) { }
}

/// <summary>椭圆柱。</summary>
[JsonTypeName("ellipse")]
public class Ellipse : Extruded
{
    public Ellipse() : base(Rasterizer.FilledEllipse(1, 1), 1) { }
    public Ellipse(int radiusX, int radiusZ, int height) : base(Rasterizer.FilledEllipse(radiusX, radiusZ), height) { }
}

/// <summary>环柱（圆形走廊、柱廊、泳池）。</summary>
[JsonTypeName("ring")]
public class Ring : Extruded
{
    public Ring() : base(Rasterizer.FilledRing(1, 2), 1) { }
    public Ring(int innerRadius, int outerRadius, int height) : base(Rasterizer.FilledRing(innerRadius, outerRadius), height) { }
}

/// <summary>圆柱——Circle 的命名别名。</summary>
[JsonTypeName("cylinder")]
public class Cylinder : Circle
{
    public Cylinder() : base() { }
    public Cylinder(int radius, int height) : base(radius, height) { }
}

/// <summary>三棱柱（楔形、倒角）：三角形截面挤出。</summary>
[JsonTypeName("triangle")]
public class Triangle : Polygon
{
    public Triangle() : base() { }
    public Triangle(Int2 a, Int2 b, Int2 c, int height) : base(new[] { a, b, c }, height) { }
}
