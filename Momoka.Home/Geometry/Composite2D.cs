using Momoka.Home.Primitives;
using Momoka.Home.Storage;
namespace Momoka.Home.Geometry;

/// <summary>Union of 2D footprints at local offsets (L/U/T plans, attached parts).</summary>
[JsonTypeName("composite")]
public class Composite2D : Shape
{
    public List<CompositeChild2D> Children { get; set; } = new();

    public override IEnumerable<Int2> Cells2D()
    {
        var seen = new HashSet<Int2>();
        foreach (var child in Children)
        {
            foreach (var cell in child.Shape.Cells2D())
            {
                var p = cell + child.Offset;
                if (seen.Add(p))
                    yield return p;
            }
        }
    }
}

/// <summary>A child footprint of a <see cref="Composite2D"/> at a local offset.</summary>
public class CompositeChild2D
{
    public Shape Shape { get; set; } = null!;
    public Int2 Offset { get; set; }
}
