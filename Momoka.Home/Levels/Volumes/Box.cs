using Momoka.Home;
using Momoka.Home.Primitives;
using Momoka.Home.Data.Json;
namespace Momoka.Home.Levels.Volumes;

[JsonTypeName("box")]
public class Box : Volume
{
    public int SizeX { get; set; } = 1;
    public int SizeY { get; set; } = 1;
    public int SizeZ { get; set; } = 1;

    /// <summary>
    /// Rasterizes the box volume (X × Y × Z) into LOCAL cells — relative to the
    /// host entity's Coords (world = Coords + cell). The shape carries no
    /// position; it only describes its own geometry.
    /// </summary>
    public override IEnumerable<Int3> Cells3D()
    {
        for (var dy = 0; dy < SizeY; dy++)
        {
            for (var dx = 0; dx < SizeX; dx++)
            {
                for (var dz = 0; dz < SizeZ; dz++)
                {
                    yield return new Int3(dx, dy, dz);
                }
            }
        }
    }
}
