using Momoka.Home.Primitives;
namespace Momoka.Home.Geometry;

/// <summary>
/// Union of 3D sub-volumes at local offsets — the primitive for irregular /
/// multi-part structures (L/U/C/T plans, house + garage, bay windows).
/// </summary>
public class Composite3D : Volume
{
    public List<(Volume Volume, Int3 Offset)> Children { get; } = new();

    public override IEnumerable<Int3> Cells3D()
    {
        var seen = new HashSet<Int3>();
        foreach (var (child, offset) in Children)
        {
            foreach (var cell in child.Cells3D())
            {
                var p = cell + offset;
                if (seen.Add(p))
                    yield return p;
            }
        }
    }

    public override IEnumerable<Int2> Cells2D()
    {
        var seen = new HashSet<Int2>();
        foreach (var (child, offset) in Children)
        {
            foreach (var cell in child.Cells2D())
            {
                var p = cell + offset.Xz;
                if (seen.Add(p))
                    yield return p;
            }
        }
    }
}
