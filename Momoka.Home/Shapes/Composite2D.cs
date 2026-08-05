using Momoka.Home.Primitives;
namespace Momoka.Home.Shapes;

/// <summary>Union of 2D footprints at local offsets (L/U/T plans, attached parts).</summary>
public class Composite2D : Shape2D
{
    public List<(Shape2D Shape, Int2 Offset)> Children { get; } = new();

    public override IEnumerable<Int2> GetCells()
    {
        var seen = new HashSet<Int2>();
        foreach (var (shape, offset) in Children)
        {
            foreach (var cell in shape.GetCells())
            {
                var p = cell + offset;
                if (seen.Add(p))
                    yield return p;
            }
        }
    }
}
