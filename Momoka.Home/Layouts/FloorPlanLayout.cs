using Momoka.Home;
using Momoka.Home.Entities;
using Momoka.Home.Primitives;
using Momoka.Home.Shapes;
namespace Momoka.Home.Layouts;

/// <summary>
/// Floor-plan boundary layout: a planar graph whose edges are the partitions
/// (walls, fences, …) that define a space's outline and interior divisions —
/// the topology of a floor plan. Handles TOPOLOGY only: positioning an edge
/// entity and registering its nodes/edge. Occupancy rasterization and collision
/// are the <see cref="VoxelLayout3D"/>'s job and are coordinated by the caller
/// (e.g. an editor command).
/// </summary>
public class FloorPlanLayout : Graph2D<Entity<Int3>>
{
    /// <summary>
    /// 创建隔断：在 <paramref name="from"/>–<paramref name="to"/> 之间建造一条分区边
    /// （墙 / 围栏 / …）：锚定分区的 LineShape（局部坐标）并注册图节点与边。
    /// 占用栅格化由调用方通过 <see cref="VoxelLayout3D.BuildAt"/> 完成。
    /// </summary>
    public bool Build(Int2 from, Int2 to, Entity<Int3> partition)
    {
        if (partition.Shape is not LineShape line)
            return false;

        partition.Coords = from.ToInt3();
        line.Start = Float3.Zero;
        line.End = (to - from).ToFloat3();

        AddNode(from);
        AddNode(to);
        AddEdge(from, to, partition);

        // Derived surfaces (wall faces…) depend on the freshly anchored line —
        // refresh them so the SurfaceSource catalog is always current.
        (partition as IRefreshableSurfaces)?.RefreshSurfaces();
        return true;
    }

    /// <summary>
    /// 拆除隔断：移除 <paramref name="from"/>–<paramref name="to"/> 之间的分区边。
    /// 图节点保留（可能被其他边共享）；占用清理由调用方通过
    /// <see cref="VoxelLayout3D.DestroyAt"/> 完成。
    /// </summary>
    public bool Destroy(Int2 from, Int2 to)
    {
        var a = TryGetNode(from);
        var b = TryGetNode(to);
        if (a is null || b is null)
            return false;

        var edge = FindEdge(a.Value, b.Value);
        if (edge is null)
            return false;

        Edges.Remove(edge.Value);
        return true;
    }
}
