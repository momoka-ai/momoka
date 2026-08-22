using Momoka.Home.Primitives;
using Momoka.Home.Data.Json;
namespace Momoka.Home.Levels.Volumes;

/// <summary>
/// Union of 3D sub-volumes at local offsets — the primitive for irregular /
/// multi-part structures (L/U/C/T plans, house + garage, bay windows).
/// </summary>
[JsonTypeName("composite")]
public class Composite : Volume
{
    public List<CompositeChild> Children { get; set; } = new();

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
}

/// <summary>A child volume of a <see cref="Composite"/> at a local offset.</summary>
public class CompositeChild
{
    public Volume Shape { get; set; } = null!;
    public Int3 Offset { get; set; }
}
