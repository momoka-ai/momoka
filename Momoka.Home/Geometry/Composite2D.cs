using Momoka.Home.Primitives;
using Momoka.Home.Serialization;
namespace Momoka.Home.Geometry;

/// <summary>Union of 2D footprints at local offsets (L/U/T plans, attached parts).</summary>
[JsonTypeName("composite")]
public class Composite2D : Shape
{
    public List<(Shape Shape, Int2 Offset)> Children { get; } = new();

    public override IEnumerable<Int2> Cells2D()
    {
        var seen = new HashSet<Int2>();
        foreach (var (shape, offset) in Children)
        {
            foreach (var cell in shape.Cells2D())
            {
                var p = cell + offset;
                if (seen.Add(p))
                    yield return p;
            }
        }
    }
}
