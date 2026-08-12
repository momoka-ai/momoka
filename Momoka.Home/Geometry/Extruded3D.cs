using Momoka.Home.Primitives;
using Momoka.Home.Data.Json;
namespace Momoka.Home.Geometry;

/// <summary>
/// A prism: a 2D footprint (<see cref="Footprint"/>) extruded vertically by
/// <see cref="Height"/> cells. Generalizes Box (rect footprint), cylinder
/// (circle footprint), polygon buildings, and more.
/// </summary>
[JsonTypeName("extruded")]
public class Extruded3D : Volume
{
    public Shape Footprint { get; set; } = new Rect2D();
    public int Height { get; set; } = 1;

    public Extruded3D() { }
    public Extruded3D(Shape footprint, int height)
    {
        Footprint = footprint;
        Height = height;
    }

    public override IEnumerable<Int3> Cells3D()
    {
        foreach (var cell in Footprint.Cells2D())
            for (var y = 0; y < Height; y++)
                yield return new Int3(cell.X, y, cell.Z);
    }

    public override IEnumerable<Int2> Cells2D() => Footprint.Cells2D();
}
