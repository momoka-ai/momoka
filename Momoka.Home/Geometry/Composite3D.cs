using Momoka.Home.Primitives;
using Momoka.Home.Serialization;
namespace Momoka.Home.Geometry;

/// <summary>
/// Union of 3D sub-volumes at local offsets — the primitive for irregular /
/// multi-part structures (L/U/C/T plans, house + garage, bay windows).
/// </summary>
[JsonTypeName("composite")]
public class Composite3D : Volume
{
    public List<CompositeChild3D> Children { get; set; } = new();

    public override IEnumerable<Int3> Cells3D()
    {
        var seen = new HashSet<Int3>();
        foreach (var child in Children)
        {
            foreach (var cell in child.Shape.Cells3D())
            {
                var p = cell + child.Offset;
                if (seen.Add(p))
                    yield return p;
            }
        }
    }

    public override IEnumerable<Int2> Cells2D()
    {
        var seen = new HashSet<Int2>();
        foreach (var child in Children)
        {
            foreach (var cell in child.Shape.Cells2D())
            {
                var p = cell + child.Offset.Xz;
                if (seen.Add(p))
                    yield return p;
            }
        }
    }
}

/// <summary>A child volume of a <see cref="Composite3D"/> at a local offset.</summary>
public class CompositeChild3D
{
    public Volume Shape { get; set; } = null!;
    public Int3 Offset { get; set; }
}
