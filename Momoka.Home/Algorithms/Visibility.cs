using Momoka.Home.Primitives;
namespace Momoka.Home.Algorithms;

/// <summary>
/// 直线几何与视野判定：点对直线的分解（<see cref="Project"/>）与点是否落在
/// 视野形状内的判定（<see cref="IsInView"/>，恒定半径的圆柱形视野；半径随距离
/// 线性扩大的锥形判定在 <see cref="Traverse.InCone"/> 内部）。
/// 纯几何——不含体素与实体概念；遮挡（视线）是另一层关注（<c>IVoxelSource.CanSee</c>）。
/// </summary>
public static class Visibility
{
    /// <summary>点对直线（origin + 单位 dir）的分解：<see cref="Distance"/> = 沿直线的
    /// 投影距离（0 在 origin 处，负值在背后），<see cref="Lateral"/> = 垂直分量向量
    /// （其长度即点到直线的垂距）。</summary>
    public readonly record struct Projection(float Distance, Float3 Lateral)
    {
        /// <summary>垂距：点到直线的垂直距离。</summary>
        public float LateralDistance => Lateral.Magnitude;
    }

    /// <summary>把点分解到过 origin、方向 dir 的直线上：Distance = 投影距离，
    /// Lateral = 垂直分量。<paramref name="dir"/> 必须为单位向量——否则 Distance 不是真实距离。</summary>
    public static Projection Project(Float3 point, Float3 origin, Float3 dir)
    {
        var offset = point - origin;
        var t = Float3.Dot(offset, dir);
        return new Projection(t, offset - dir * t);
    }

    /// <summary>点是否落在圆柱形视野内：沿 <paramref name="dir"/> 的投影距离在
    /// [0, maxDistance] 且垂距 ≤ maxRadius（半径恒定，不随距离变化——若需随距离
    /// 线性扩大的锥形，见 <see cref="Traverse.InCone"/>）。<paramref name="dir"/>
    /// 必须为单位向量（调用方归一化，如 <c>CanSee</c> 内部已处理）。
    /// 纯几何判定，不含遮挡。</summary>
    public static bool IsInView(Float3 point, Float3 origin, Float3 dir, float maxDistance, float maxRadius)
    {
        var p = Project(point, origin, dir);
        return p.Distance >= 0 && p.Distance <= maxDistance && p.LateralDistance <= maxRadius;
    }
}
