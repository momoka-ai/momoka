using Momoka.Home.Primitives;

namespace Momoka.Home;

/// <summary>
/// 2D boundary layout: a planar graph whose edges are partitions (walls, fences,
/// …). Owns build/demolish of edge entities, keeping the graph, the occupancy
/// grid, and the entity list in sync. The graph itself is inherited from
/// <see cref="Graph2D{TEntity}"/>; the occupancy container is composed in.
/// </summary>
public class GraphLayout2D : Graph2D<VoxelEntity>
{
    private readonly VoxelLayout3D _occupancy;

    public GraphLayout2D(VoxelLayout3D occupancy) => _occupancy = occupancy;

    /// <summary>
    /// 创建隔断：在 <paramref name="from"/>–<paramref name="to"/> 之间建造一条分区边
    /// （墙 / 围栏 / …）：锚定分区（LineShape 为局部）、注册图节点与边，并把分区
    /// 栅格化到占用网格。
    /// </summary>
    public bool BuildPartition(Int2 from, Int2 to, VoxelEntity partition)
    {
        if (partition.Shape is not LineShape line)
            return false;

        partition.Coords = from.ToInt3();
        line.Start = Float3.Zero;
        line.End = (to - from).ToFloat3();

        AddNode(from);
        AddNode(to);
        AddEdge(from, to, partition);

        foreach (var cell in line.GetVoxels())
        {
            _occupancy[partition.Coords + cell] = partition;
        }
        _occupancy.Entities.Add(partition);
        return true;
    }

    /// <summary>
    /// 拆除隔断：移除 <paramref name="from"/>–<paramref name="to"/> 之间的分区边，
    /// 清空其占用体素并注销实体。图节点保留（可能被其他边共享）。
    /// </summary>
    public bool DemolishPartition(Int2 from, Int2 to)
    {
        var a = TryGetNode(from);
        var b = TryGetNode(to);
        if (a is null || b is null)
            return false;

        var edge = FindEdge(a.Value, b.Value);
        if (edge is null || edge.Value.Entity is not VoxelEntity partition)
            return false;

        Edges.Remove(edge.Value);
        foreach (var cell in partition.Shape.GetVoxels())
        {
            var pos = partition.Coords + cell;
            if (_occupancy[pos] == partition)
                _occupancy[pos] = null;
        }
        _occupancy.Entities.Remove(partition);
        return true;
    }
}
