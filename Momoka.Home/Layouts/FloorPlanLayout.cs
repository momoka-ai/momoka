using Momoka.Home;
using Momoka.Home.Entities;
using Momoka.Home.Geometry;
using Momoka.Home.Primitives;
namespace Momoka.Home.Layouts;

/// <summary>
/// Floor-plan boundary layout: a planar graph whose edges are the partitions
/// (walls, fences, …) that define a space's outline and interior divisions —
/// the topology of a floor plan. Positions an edge entity, registers its
/// nodes/edge, and derives each partition's placement surfaces on demand
/// (<see cref="Surfaces"/>) from the graph itself — never stale. Occupancy
/// rasterization and collision are the <see cref="VoxelLayout{T}"/>'s job and
/// are coordinated by the caller (e.g. an editor command).
/// </summary>
public class FloorPlanLayout : Graph2D<Entity<Int3>>
{
    /// <summary>Property-table key: whether a partition exposes placement faces.</summary>
    public const string UseVoxelLayoutProperty = "use_voxel_layout";
    /// <summary>Property-table key: partition height in cells — the face extent.</summary>
    public const string HeightProperty = "height";
    /// <summary>Property-table key: partition thickness in cells — the second face's offset.</summary>
    public const string ThicknessProperty = "thickness";

    /// <summary>
    /// 创建隔断：在 <paramref name="from"/>–<paramref name="to"/> 之间建造一条分区边
    /// （墙 / 围栏 / …）：锚定分区的 Line3D（局部坐标）并注册图节点与边。
    /// 占用栅格化由调用方通过 <see cref="VoxelLayout{T}.BuildAt"/> 完成。
    /// </summary>
    public bool Build(Int2 from, Int2 to, Entity<Int3> partition)
    {
        if (partition.Volume is not Line3D line)
            return false;

        partition.Coords = from.ToInt3();
        line.Start = Float3.Zero;
        line.End = (to - from).ToFloat3();

        AddNode(from);
        AddNode(to);
        AddEdge(from, to, partition);
        return true;
    }

    /// <summary>
    /// 拆除隔断：移除 <paramref name="from"/>–<paramref name="to"/> 之间的分区边。
    /// 图节点保留（可能被其他边共享）；占用清理由调用方通过
    /// <see cref="VoxelLayout{T}.DestroyAt"/> 完成。
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

    /// <summary>
    /// Every placement surface of this floor plan: for each partition edge whose
    /// entity declares <c>use_voxel_layout = true</c>, the two faces derived from
    /// the edge's span (length comes from the graph nodes) and the entity's
    /// <c>height</c>/<c>thickness</c> properties. Computed on demand — never
    /// stale. Axis-aligned partitions yield two faces; diagonal ones none.
    /// </summary>
    public IEnumerable<VoxelLayout2D> Surfaces
    {
        get
        {
            foreach (var edge in Edges)
            {
                if (edge.Entity is not Entity<Int3> partition)
                    continue;
                if (partition.Volume is not Line3D line)
                    continue;
                if (!partition.TryGetValue(UseVoxelLayoutProperty, out var enabled) || enabled is not true)
                    continue;

                var height = ReadInt(partition, HeightProperty, 3);
                var thickness = Math.Max(1, ReadInt(partition, ThicknessProperty, 1));
                foreach (var face in ComputeFaces(edge, partition, height, thickness))
                    yield return face;
            }
        }
    }

    private static IEnumerable<VoxelLayout2D> ComputeFaces(Edge edge, Entity<Int3> partition, int height, int thickness)
    {
        var y = partition.Coords.Y;
        var a = new Int3(edge.A.Coords.X, y, edge.A.Coords.Z);
        var b = new Int3(edge.B.Coords.X, y, edge.B.Coords.Z);
        var length = Math.Max(Math.Abs(b.X - a.X), Math.Abs(b.Z - a.Z));
        var x0 = Math.Min(a.X, b.X);
        var z0 = Math.Min(a.Z, b.Z);

        if (a.Z == b.Z)
        {
            // East–West partition → South (−Z) and North (+Z) faces
            yield return new VoxelLayout2D(new Int2(length, height), new Int3(x0, y, z0)) { Direction = Int3.South };
            yield return new VoxelLayout2D(new Int2(length, height), new Int3(x0, y, z0 + thickness)) { Direction = Int3.North };
        }
        else if (a.X == b.X)
        {
            // North–South partition → West (−X) and East (+X) faces
            yield return new VoxelLayout2D(new Int2(height, length), new Int3(x0, y, z0)) { Direction = Int3.West };
            yield return new VoxelLayout2D(new Int2(height, length), new Int3(x0 + thickness, y, z0)) { Direction = Int3.East };
        }
        // Diagonal partitions: axis-aligned Direction cannot express the face normal — no faces.
    }

    private static int ReadInt(Entity<Int3> entity, string name, int fallback) =>
        entity.TryGetValue(name, out var value) && value is int i ? i : fallback;
}
