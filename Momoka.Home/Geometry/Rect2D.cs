using Momoka.Home.Primitives;
using Momoka.Home.Data.Json;
namespace Momoka.Home.Geometry;

/// <summary>Axis-aligned rectangle footprint (SizeX × SizeZ), the Shape of <see cref="Box3D"/>.</summary>
[JsonTypeName("rect")]
public class Rect2D : Shape
{
    public int SizeX { get; set; } = 1;
    public int SizeZ { get; set; } = 1;

    public Rect2D() { }
    public Rect2D(int sizeX, int sizeZ)
    {
        SizeX = sizeX;
        SizeZ = sizeZ;
    }

    public override IEnumerable<Int2> Cells2D()
    {
        for (var x = 0; x < SizeX; x++)
            for (var z = 0; z < SizeZ; z++)
                yield return new Int2(x, z);
    }
}
