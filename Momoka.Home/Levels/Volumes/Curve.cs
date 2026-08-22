using Momoka.Home.Primitives;
using Momoka.Home.Data.Json;
namespace Momoka.Home.Levels.Volumes;

/// <summary>
/// A curved wall segment: a quadratic Bézier arc through <see cref="Line.Start"/>,
/// <see cref="Line.End"/> and a bowed midpoint (Start+End)/2 + perpendicular·Curvature.
/// Curvature = 0 degenerates to a straight line (same as <see cref="Line"/>);
/// positive/negative bows to either side. Rasterized by sampling the curve and
/// expanding by <see cref="Line.Thickness"/>.
/// </summary>
[JsonTypeName("curve")]
public class Curve : Line
{
    /// <summary>Signed bow distance (cells) at the midpoint, perpendicular to the chord.</summary>
    public float Curvature { get; set; }

    public override IEnumerable<Int3> Cells3D()
    {
        var dx = End.X - Start.X;
        var dz = End.Z - Start.Z;
        var chord = Math.Sqrt(dx * dx + dz * dz);
        if (chord < 0.001)
        {
            foreach (var pos in RasterizeCross(Start, Thickness))
                yield return pos.AsInt3();
            yield break;
        }

        var dirX = dx / chord;
        var dirZ = dz / chord;
        var perpX = -dirZ;
        var perpZ = dirX;
        var midX = (Start.X + End.X) / 2f;
        var midZ = (Start.Z + End.Z) / 2f;
        var ctrlX = midX + perpX * Curvature;
        var ctrlZ = midZ + perpZ * Curvature;

        var steps = Math.Max(8, (int)Math.Ceiling(chord * 2));
        for (var i = 0; i <= steps; i++)
        {
            var t = i / (double)steps;
            var u = 1 - t;
            var x = (float)(u * u * Start.X + 2 * u * t * ctrlX + t * t * End.X);
            var z = (float)(u * u * Start.Z + 2 * u * t * ctrlZ + t * t * End.Z);
            var center = new Float3(x, Start.Y, z);

            foreach (var pos in RasterizeCross(center, Thickness))
                yield return pos.AsInt3();
        }
    }

}
