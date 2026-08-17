using Momoka.Home;
using Momoka.Home.Entities;
using Momoka.Home.Layouts;
using Momoka.Home.Primitives;
namespace Momoka.Home.Geometry;
/// <summary>
/// The 3D counterpart of <see cref="Shape"/>: a volume occupying voxel cells in
/// local 3D space, relative to the host's Coords. Volumes carry no position (that
/// lives on the host entity); they only describe their own geometry. Implements
/// <see cref="IVoxelGeometry3D"/> (its own 3D cells) and
/// <see cref="IVoxelGeometry2D"/> (its projection onto the local XZ plane).
/// Placement into an entity grid goes through the host
/// (<c>VoxelLayout&lt;Entity&gt;.BuildAt(Entity, pos)</c>), while
/// <see cref="UnitLayout"/> places hosts into the root grid.
/// </summary>
public abstract class Volume : IVoxelGeometry3D, IVoxelGeometry2D
{
    /// <summary>Local occupied 3D cells, relative to the host's Coords (snapped to the grid).</summary>
    public abstract IEnumerable<Int3> Cells3D();

    /// <summary>
    /// The volume's 2D projection — its support footprint on the local XZ plane,
    /// the face that contacts the placement surface.
    /// </summary>
    public abstract IEnumerable<Int2> Cells2D();

    /// <summary>
    /// A bare volume has no host identity, so it cannot place itself into an entity
    /// grid. Place the host instead: <c>VoxelLayout&lt;Entity&gt;.BuildAt(Entity, pos)</c>.
    /// <see cref="UnitLayout"/> implements the real placement contract
    /// (PlaceAt/DestroyAt).
    /// </summary>
    public void PlaceAt(VoxelLayout<Entity> target, Int3 at) =>
        throw new NotSupportedException("A bare volume has no host identity — place it via VoxelLayout&lt;Entity&gt;.BuildAt(Entity, pos) or via UnitLayout.");

    /// <summary>
    /// 两个体积是否重叠：本形状锚定在 <paramref name="anchor"/>、other 锚定在
    /// <paramref name="otherAnchor"/>（均为格坐标），占用格交集非空即重叠。
    /// 纯几何判定——实体对碰撞（<c>UnitLayout.IsCollided</c>）即委托本方法。
    /// </summary>
    public bool Intersects(Int3 anchor, Volume other, Int3 otherAnchor)
    {
        var cells = other.Cells3D().Select(c => otherAnchor + c).ToHashSet();
        return Cells3D().Any(c => cells.Contains(anchor + c));
    }

    /// <inheritdoc cref="PlaceAt"/>
    public void DestroyAt(VoxelLayout<Entity> target, Int3 at) =>
        throw new NotSupportedException("A bare volume has no host identity — remove it via VoxelLayout&lt;Entity&gt;.DestroyAt(pos) or via UnitLayout.");
}
