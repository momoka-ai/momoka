using Momoka.Home.Primitives;
using Momoka.Home.Storage;
namespace Momoka.Home.Geometry;

/// <summary>Filled circle footprint: cells with x²+z² ≤ r².</summary>
[JsonTypeName("circle")]
public class Circle2D : Shape
{
    public int Radius { get; set; } = 1;

    public Circle2D() { }
    public Circle2D(int radius) => Radius = radius;

    public override IEnumerable<Int2> Cells2D()
    {
        var r = Radius;
        for (var x = -r; x <= r; x++)
            for (var z = -r; z <= r; z++)
                if (x * x + z * z <= r * r)
                    yield return new Int2(x, z);
    }
}

/// <summary>Filled ellipse footprint: (x/rx)² + (z/rz)² ≤ 1.</summary>
[JsonTypeName("ellipse")]
public class Ellipse2D : Shape
{
    public int RadiusX { get; set; } = 1;
    public int RadiusZ { get; set; } = 1;

    public Ellipse2D() { }
    public Ellipse2D(int radiusX, int radiusZ)
    {
        RadiusX = radiusX;
        RadiusZ = radiusZ;
    }

    public override IEnumerable<Int2> Cells2D()
    {
        var a = RadiusX;
        var b = RadiusZ;
        for (var x = -a; x <= a; x++)
            for (var z = -b; z <= b; z++)
                if (x * x * b * b + z * z * a * a <= a * a * b * b)
                    yield return new Int2(x, z);
    }
}

/// <summary>Annulus footprint: inner² ≤ x²+z² ≤ outer² (circular corridors, colonnades, pools).</summary>
[JsonTypeName("ring")]
public class Ring2D : Shape
{
    public int InnerRadius { get; set; } = 1;
    public int OuterRadius { get; set; } = 2;

    public Ring2D() { }
    public Ring2D(int innerRadius, int outerRadius)
    {
        InnerRadius = innerRadius;
        OuterRadius = outerRadius;
    }

    public override IEnumerable<Int2> Cells2D()
    {
        var outer = OuterRadius;
        var inner2 = InnerRadius * InnerRadius;
        for (var x = -outer; x <= outer; x++)
        {
            for (var z = -outer; z <= outer; z++)
            {
                var d2 = x * x + z * z;
                if (d2 >= inner2 && d2 <= outer * outer)
                    yield return new Int2(x, z);
            }
        }
    }
}
