using Momoka.Home.Primitives;

namespace Momoka.Home.Models.Shapes;

public class LineShape : Shape
{
    /// <summary>Start of the segment, in the host entity's LOCAL frame (relative to Coords).</summary>
    public Float3 Start { get; set; }
    /// <summary>End of the segment, in the host entity's LOCAL frame.</summary>
    public Float3 End { get; set; }
    public float Curvature { get; set; }
    public int Thickness { get; set; } = 1;

    /// <summary>Rasterizes the line (with thickness) into grid cells.</summary>
    public override IEnumerable<Int3> GetVoxels()
    {
        var dx = End.X - Start.X;
        var dz = End.Z - Start.Z;
        var steps = (int)Math.Max(Math.Abs(dx), Math.Abs(dz));

        if (steps == 0)
        {
            foreach (var pos in RasterizeCross(Start, Thickness))
                yield return pos.Int3;
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
                yield return pos.Int3;
        }
    }

    /// <summary>
    /// Support footprint: the line projected onto its local XZ plane (drop Y).
    /// A wall's support footprint is its own XZ extent.
    /// </summary>
    public override IEnumerable<Int2> GetVoxelsOnAngle()
    {
        foreach (var voxel in GetVoxels())
            yield return new Int2(voxel.X, voxel.Z);
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
