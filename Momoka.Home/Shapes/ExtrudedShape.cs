using Momoka.Home.Primitives;
namespace Momoka.Home.Shapes;

/// <summary>
/// A prism: a 2D footprint (<see cref="Footprint"/>) extruded vertically by
/// <see cref="Height"/> cells. Generalizes Box (rect footprint), cylinder
/// (circle footprint), polygon buildings, and more.
/// </summary>
public class ExtrudedShape : Shape
{
    public Shape2D Footprint { get; set; } = new Rect2D();
    public int Height { get; set; } = 1;

    public ExtrudedShape() { }
    public ExtrudedShape(Shape2D footprint, int height)
    {
        Footprint = footprint;
        Height = height;
    }

    public override IEnumerable<Int3> Cells()
    {
        foreach (var cell in Footprint.GetCells())
            for (var y = 0; y < Height; y++)
                yield return new Int3(cell.X, y, cell.Z);
    }

    public override IEnumerable<Int2> GetVoxelsOnAngle() => Footprint.GetCells();
}
