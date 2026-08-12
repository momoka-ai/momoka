using Momoka.Home.Primitives;
using Momoka.Home.Data.Json;
namespace Momoka.Home.Geometry;

/// <summary>Vertical cone: circular base shrinking linearly to an apex (roofs, spires).</summary>
[JsonTypeName("cone")]
public class Cone3D : Volume
{
    public int Radius { get; set; } = 1;
    public int Height { get; set; } = 1;

    public Cone3D() { }
    public Cone3D(int radius, int height)
    {
        Radius = radius;
        Height = height;
    }

    public override IEnumerable<Int3> Cells3D()
    {
        for (var y = 0; y < Height; y++)
        {
            var ry = Radius * (Height - y) / (double)Height;
            for (var x = -Radius; x <= Radius; x++)
            {
                for (var z = -Radius; z <= Radius; z++)
                {
                    if (x * x + z * z <= ry * ry)
                        yield return new Int3(x, y, z);
                }
            }
        }
    }

    public override IEnumerable<Int2> Cells2D() => new Circle2D(Radius).Cells2D();
}

/// <summary>Vertical pyramid: rectangular base shrinking linearly to an apex (gable/pyramid roofs).</summary>
[JsonTypeName("pyramid")]
public class Pyramid3D : Volume
{
    public int SizeX { get; set; } = 1;
    public int SizeZ { get; set; } = 1;
    public int Height { get; set; } = 1;

    public Pyramid3D() { }
    public Pyramid3D(int sizeX, int sizeZ, int height)
    {
        SizeX = sizeX;
        SizeZ = sizeZ;
        Height = height;
    }

    public override IEnumerable<Int3> Cells3D()
    {
        for (var y = 0; y < Height; y++)
        {
            var wx = Math.Max(1, (int)Math.Round(SizeX * (Height - y) / (double)Height));
            var wz = Math.Max(1, (int)Math.Round(SizeZ * (Height - y) / (double)Height));
            var x0 = -wx / 2;
            var z0 = -wz / 2;
            for (var dx = 0; dx < wx; dx++)
                for (var dz = 0; dz < wz; dz++)
                    yield return new Int3(x0 + dx, y, z0 + dz);
        }
    }

    public override IEnumerable<Int2> Cells2D() =>
        Cells3D().Where(c => c.Y == 0).Select(c => c.Xz).Distinct();
}
