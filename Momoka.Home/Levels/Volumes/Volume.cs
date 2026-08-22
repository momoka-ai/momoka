using Momoka.Home.Levels.Layouts;
using Momoka.Home.Primitives;
namespace Momoka.Home.Levels.Volumes;
/// <summary>
/// 3D 体积：在局部 3D 空间中占据体素格，相对宿主 Coords。体积不携带位置
/// （位置在宿主实体上），只描述自身几何。放置经宿主
/// （<c>LevelLayout.Add(entity, position)</c>）。实现 <see cref="IVoxelSet"/>
/// 以暴露占用格集合（<see cref="GetVoxelSet"/>）。
/// </summary>
public abstract class Volume : IVoxelSet
{
    /// <inheritdoc/>
    public abstract IEnumerable<Int3> GetVoxelSet();

    /// <summary>
    /// 两个体积是否重叠：本形状锚定在 <paramref name="anchor"/>、other 锚定在
    /// <paramref name="otherAnchor"/>（均为格坐标），占用格交集非空即重叠。
    /// 纯几何判定——实体对碰撞（<c>LevelLayout.IsCollided</c>）即委托本方法。
    /// </summary>
    public bool Intersects(Int3 anchor, Volume other, Int3 otherAnchor)
    {
        var cells = other.GetVoxelSet().Select(c => otherAnchor + c).ToHashSet();
        return GetVoxelSet().Any(c => cells.Contains(anchor + c));
    }
}
