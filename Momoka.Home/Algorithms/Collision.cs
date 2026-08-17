using Momoka.Home.Primitives;
namespace Momoka.Home.Algorithms;

/// <summary>
/// 碰撞查询（<c>IVoxelSource.IsCollided</c> 系列）与视野内目标
/// （<c>FindItemsInView</c>）共用的命中结果。不命中时扩展方法返回 null，
/// 故结果无需 Collided 标志。命中点语义：静态碰撞 = 格中心；
/// 直线扫描 = 直线进入格面的精确交点；锥体扫描 = 格中心到直线轴的投影点。
/// </summary>
public static class Collision
{
    /// <summary>一次碰撞命中：撞到的实体、所在格与命中点（世界 cm）。</summary>
    public readonly record struct Result<T>(T Hit, Int3 Cell, Position Point)
        where T : notnull;
}
