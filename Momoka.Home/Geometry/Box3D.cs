using Momoka.Home;
using Momoka.Home.Primitives;
namespace Momoka.Home.Geometry;

public class Box3D : Volume
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

    /// <summary>
    /// Support footprint: the box's extent in its local XZ plane — the face that
    /// contacts the surface. For a 2(thickness)×3(height)×3(width) cabinet this
    /// is 2×3. The placement orientates the box onto the surface, so its local XZ
    /// maps to the surface plane (floor → world XZ, wall East/West → world YZ,
    /// wall North/South → world XY). Cells are LOCAL (relative to Coords).
    /// </summary>
    public override IEnumerable<Int2> Cells2D()
    {
        for (var dx = 0; dx < SizeX; dx++)
        {
            for (var dz = 0; dz < SizeZ; dz++)
            {
                yield return new Int2(dx, dz);
            }
        }
    }
}
