using Momoka.Home.Primitives;
using Momoka.Home.Data.Json;
namespace Momoka.Home.Levels.Volumes;

/// <summary>圆柱（圆塔、花池、喷泉）：圆形截面挤出。</summary>
[JsonTypeName("circle")]
public class Circle3D : Extruded3D
{
    public Circle3D() : base(Rasterizer.FilledCircle(1), 1) { }
    public Circle3D(int radius, int height) : base(Rasterizer.FilledCircle(radius), height) { }
}

/// <summary>椭圆柱。</summary>
[JsonTypeName("ellipse")]
public class Ellipse3D : Extruded3D
{
    public Ellipse3D() : base(Rasterizer.FilledEllipse(1, 1), 1) { }
    public Ellipse3D(int radiusX, int radiusZ, int height) : base(Rasterizer.FilledEllipse(radiusX, radiusZ), height) { }
}

/// <summary>环柱（圆形走廊、柱廊、泳池）。</summary>
[JsonTypeName("ring")]
public class Ring3D : Extruded3D
{
    public Ring3D() : base(Rasterizer.FilledRing(1, 2), 1) { }
    public Ring3D(int innerRadius, int outerRadius, int height) : base(Rasterizer.FilledRing(innerRadius, outerRadius), height) { }
}

/// <summary>圆柱——Circle3D 的命名别名。</summary>
[JsonTypeName("cylinder")]
public class Cylinder3D : Circle3D
{
    public Cylinder3D() : base() { }
    public Cylinder3D(int radius, int height) : base(radius, height) { }
}

/// <summary>三棱柱（楔形、倒角）：三角形截面挤出。</summary>
[JsonTypeName("triangle")]
public class Triangle3D : Polygon3D
{
    public Triangle3D() : base() { }
    public Triangle3D(Int2 a, Int2 b, Int2 c, int height) : base(new[] { a, b, c }, height) { }
}
