using Momoka.Home.Primitives;
using Momoka.Home.Data.Json;
namespace Momoka.Home.Geometry;

/// <summary>Solid sphere: cells with x²+y²+z² ≤ r².</summary>
[JsonTypeName("sphere")]
public class Sphere3D : Volume
{
    public int Radius { get; set; } = 1;

    public Sphere3D() { }
    public Sphere3D(int radius) => Radius = radius;

    public override IEnumerable<Int3> Cells3D()
    {
        var r = Radius;
        for (var x = -r; x <= r; x++)
            for (var y = -r; y <= r; y++)
                for (var z = -r; z <= r; z++)
                    if (x * x + y * y + z * z <= r * r)
                        yield return new Int3(x, y, z);
    }

}

/// <summary>Solid ellipsoid: (x/rx)² + (y/ry)² + (z/rz)² ≤ 1.</summary>
[JsonTypeName("ellipsoid")]
public class Ellipsoid3D : Volume
{
    public int RadiusX { get; set; } = 1;
    public int RadiusY { get; set; } = 1;
    public int RadiusZ { get; set; } = 1;

    public Ellipsoid3D() { }
    public Ellipsoid3D(int radiusX, int radiusY, int radiusZ)
    {
        RadiusX = radiusX;
        RadiusY = radiusY;
        RadiusZ = radiusZ;
    }

    public override IEnumerable<Int3> Cells3D()
    {
        var a = RadiusX;
        var b = RadiusY;
        var c = RadiusZ;
        var a2 = a * a;
        var b2 = b * b;
        var c2 = c * c;
        for (var x = -a; x <= a; x++)
            for (var y = -b; y <= b; y++)
                for (var z = -c; z <= c; z++)
                    if (x * x * b2 * c2 + y * y * a2 * c2 + z * z * a2 * b2 <= a2 * b2 * c2)
                        yield return new Int3(x, y, z);
    }

}
