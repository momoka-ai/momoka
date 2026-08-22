using Momoka.Home.Primitives;
using Momoka.Home.Data.Json;
namespace Momoka.Home.Levels.Volumes;

/// <summary>
/// A prism: a 2D section (<see cref="SectionCells"/>) extruded vertically by
/// <see cref="Height"/> cells. Generalizes Box (rect section), cylinder
/// (circle section), polygon buildings, and more. 截面为格数据而非独立 2D 类型。
/// </summary>
[JsonTypeName("extruded")]
public class Extruded : Volume
{
    /// <summary>截面占用格（局部 XZ，相对体积原点）；Cells3D = 截面按 Height 挤出。</summary>
    public List<Int2> SectionCells { get; set; } = new();
    public int Height { get; set; } = 1;

    public Extruded() { }
    public Extruded(IEnumerable<Int2> sectionCells, int height)
    {
        SectionCells = sectionCells.ToList();
        Height = height;
    }

    public override IEnumerable<Int3> Cells3D()
    {
        foreach (var cell in SectionCells)
            for (var y = 0; y < Height; y++)
                yield return new Int3(cell.X, y, cell.Z);
    }
}
