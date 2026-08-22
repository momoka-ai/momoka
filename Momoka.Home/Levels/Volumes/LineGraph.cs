using Momoka.Home;
using Momoka.Home.Data.Json;
using Momoka.Home.Primitives;
namespace Momoka.Home.Levels.Volumes;

/// <summary>
/// 线图墙体积（连续墙体 = 一个实体的 Volume）：继承 <see cref="Composite"/>，
/// 每个子体积是一条 <see cref="Line"/> 墙段（中心线 + 截面厚度），并按
/// <see cref="Height"/> 沿 Y 挤出。额外维护节点 / 边表（<see cref="Nodes"/> /
/// <see cref="Edges"/>）表达墙段连接关系——供延伸 / 合并墙体的模型操作定位连接点，
/// 以及未来的区域连通 / 图遍历。所有坐标相对宿主锚点（实体 Transform.Position，
/// 即体积的局部原点）。新增墙 = 图加节点 + 边 → <c>SetVolume</c>。
/// </summary>
[JsonTypeName("line_graph")]
public class LineGraph : Composite
{
    /// <summary>墙段高度（Y 向挤出，格）；所有边共享。</summary>
    public int Height { get; set; } = 1;

    /// <summary>节点表：墙段端点（相对锚点的格坐标），AddSegment 按坐标去重。</summary>
    public List<Int3> Nodes { get; set; } = new();

    /// <summary>边表：节点索引对（第 i 条边 = <c>Children[i]</c> 的 Line）。</summary>
    public List<EdgeIndex> Edges { get; set; } = new();

    public override IEnumerable<Int3> Cells3D()
    {
        var seen = new HashSet<Int3>();
        for (var i = 0; i < Children.Count; i++)
        {
            if (Children[i].Shape is not Line line)
                continue;
            var offset = Children[i].Offset;
            foreach (var cell in line.Cells3D())
            {
                for (var y = 0; y < Height; y++)
                {
                    var p = cell + offset + new Int3(0, y, 0);
                    if (seen.Add(p))
                        yield return p;
                }
            }
        }
    }

    /// <summary>
    /// 加一条墙段：登记节点（按坐标去重）→ 追加边表 → 追加 Line 子体积。
    /// 段坐标相对锚点；轴对齐 / 幅界等校验由调用方（模型操作）先行完成。
    /// </summary>
    public void AddSegment(Int3 from, Int3 to, int thickness)
    {
        var fromIdx = AddNode(from);
        var toIdx = AddNode(to);
        Edges.Add(new EdgeIndex(fromIdx, toIdx));
        Children.Add(new CompositeChild
        {
            Offset = Int3.Zero,
            Shape = new Line
            {
                Start = from.ToFloat3(),
                End = to.ToFloat3(),
                Thickness = thickness,
            },
        });
    }

    private int AddNode(Int3 coords)
    {
        var idx = Nodes.FindIndex(c => c == coords);
        if (idx >= 0)
            return idx;
        Nodes.Add(coords);
        return Nodes.Count - 1;
    }
}

/// <summary>线图边：节点索引对（指向 <see cref="LineGraph.Nodes"/>，与 Children 索引对齐）。</summary>
public readonly record struct EdgeIndex(int From, int To);
