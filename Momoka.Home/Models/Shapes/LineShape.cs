using Momoka.Home.Primitives;

namespace Momoka.Home.Models.Shapes;

public class LineShape : Shape
{
    public Float3 Start { get; set; }
    public Float3 End { get; set; }
    public float Curvature { get; set; }
    public int Thickness { get; set; } = 1;

    public override IEnumerable<Float3> Locations()
    {
        var dx = End.X - Start.X;
        var dz = End.Z - Start.Z;
        var steps = (int)Math.Max(Math.Abs(dx), Math.Abs(dz));

        if (steps == 0)
        {
            foreach (var pos in RasterizeCross(Start, Thickness))
                yield return pos;
            yield break;
        }

        var stepX = dx / steps;
        var stepZ = dz / steps;

        for (var i = 0; i <= steps; i++)
        {
            var x = (int)Math.Round(Start.X + stepX * i);
            var z = (int)Math.Round(Start.Z + stepZ * i);
            var center = new Float3(x, Start.Y, z);

            foreach (var pos in RasterizeCross(center, Thickness))
                yield return pos;
        }
    }

    private static IEnumerable<Float3> RasterizeCross(Float3 center, int thickness)
    {
        var half = thickness / 2;
        for (var dx = -half; dx <= half; dx++)
        {
            for (var dz = -half; dz <= half; dz++)
            {
                yield return center.Offset(dx, 0, dz);
            }
        }
    }
}
