using Momoka.Home.Primitives;
namespace Momoka.Home.Shapes;

/// <summary>
/// Union of 3D sub-shapes at local offsets — the primitive for irregular /
/// multi-part structures (L/U/C/T plans, house + garage, bay windows).
/// </summary>
public class CompositeShape : Shape
{
    public List<(Shape Shape, Int3 Offset)> Children { get; } = new();

    public override IEnumerable<Int3> Cells()
    {
        var seen = new HashSet<Int3>();
        foreach (var (shape, offset) in Children)
        {
            foreach (var cell in shape.Cells())
            {
                var p = cell + offset;
                if (seen.Add(p))
                    yield return p;
            }
        }
    }

    public override IEnumerable<Int2> GetVoxelsOnAngle()
    {
        var seen = new HashSet<Int2>();
        foreach (var (shape, offset) in Children)
        {
            foreach (var cell in shape.GetVoxelsOnAngle())
            {
                var p = cell + offset.Xz;
                if (seen.Add(p))
                    yield return p;
            }
        }
    }
}
