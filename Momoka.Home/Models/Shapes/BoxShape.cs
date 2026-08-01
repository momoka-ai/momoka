using Momoka.Home.Primitives;

namespace Momoka.Home.Models.Shapes;

public class BoxShape : Shape
{
    public Float3 Origin { get; set; }
    public int SizeX { get; set; } = 1;
    public int SizeZ { get; set; } = 1;

    public override IEnumerable<Float3> Locations()
    {
        for (var dx = 0; dx < SizeX; dx++)
        {
            for (var dz = 0; dz < SizeZ; dz++)
            {
                yield return Origin.Offset(dx, 0, dz);
            }
        }
    }
}
