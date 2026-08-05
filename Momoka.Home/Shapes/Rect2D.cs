using Momoka.Home.Primitives;
namespace Momoka.Home.Shapes;

/// <summary>Axis-aligned rectangle footprint (SizeX × SizeZ), the Shape2D of <see cref="BoxShape"/>.</summary>
public class Rect2D : Shape2D
{
    public int SizeX { get; set; } = 1;
    public int SizeZ { get; set; } = 1;

    public Rect2D() { }
    public Rect2D(int sizeX, int sizeZ)
    {
        SizeX = sizeX;
        SizeZ = sizeZ;
    }

    public override IEnumerable<Int2> GetCells()
    {
        for (var x = 0; x < SizeX; x++)
            for (var z = 0; z < SizeZ; z++)
                yield return new Int2(x, z);
    }
}
