using Momoka.Home.Primitives;
namespace Momoka.Home.Algorithms;

/// <summary>
/// 形状格遍历（traversal）：在体素网格上枚举形状覆盖的格序列，由近及远。
/// 输入形状参数 + 网格参数（格长 <c>length</c> 与取整函数 <c>snap</c>），
/// 输出 <c>(格, 精确点)</c> 序列——纯算法，不含体素与实体概念。
/// 直线（<see cref="OnLine"/>）为惰性 DDA 遍历；锥体（<see cref="InCone"/>）与
/// 视锥（<see cref="InFrustum"/>）为包围盒扫描 + 形状判定 + 排序（无法惰性早停，
/// 见方法 remarks）。更多形状以后续方法按同格式加入，
/// 判定基于 <see cref="Visibility"/> 的直线几何（点线分解）。
/// </summary>
public static class Traverse
{
    /// <summary>
    /// 直线遍历：按穿越顺序产出 from → to 线段经过的每个格及其精确进入点
    /// （世界 cm）——Amanatides &amp; Woo DDA（格中心取整）。每轴维护自己的
    /// 步长 / 下一格线距离 / 步进增量，每轮推进格线距离最小的轴。
    /// <paramref name="snap"/> 为"世界坐标 → 格"的取整函数（网格的 GetAsRelative），
    /// <paramref name="length"/> 为格边长（cm）。
    /// </summary>
    public static IEnumerable<(Int3 Cell, Float3 Entry)> OnLine(Float3 from, Float3 to, float length, Func<Float3, Int3> snap)
    {
        var cell = snap(from);
        var end = snap(to);
        var dir = to - from;

        var step = new Int3(Math.Sign(dir.X), Math.Sign(dir.Y), Math.Sign(dir.Z));

        var tMaxX = step.X == 0 ? double.PositiveInfinity : ((cell.X + step.X * 0.5) * length - from.X) / dir.X;
        var tMaxY = step.Y == 0 ? double.PositiveInfinity : ((cell.Y + step.Y * 0.5) * length - from.Y) / dir.Y;
        var tMaxZ = step.Z == 0 ? double.PositiveInfinity : ((cell.Z + step.Z * 0.5) * length - from.Z) / dir.Z;

        var tDeltaX = step.X == 0 ? double.PositiveInfinity : Math.Abs(length / dir.X);
        var tDeltaY = step.Y == 0 ? double.PositiveInfinity : Math.Abs(length / dir.Y);
        var tDeltaZ = step.Z == 0 ? double.PositiveInfinity : Math.Abs(length / dir.Z);

        var entry = from;
        while (true)
        {
            yield return (cell, entry);
            if (cell == end)
                yield break;

            if (tMaxX < tMaxY && tMaxX < tMaxZ)
            {
                cell = cell.Offset(step.X, 0, 0);
                entry = from + dir * (float)tMaxX;
                tMaxX += tDeltaX;
            }
            else if (tMaxY < tMaxZ)
            {
                cell = cell.Offset(0, step.Y, 0);
                entry = from + dir * (float)tMaxY;
                tMaxY += tDeltaY;
            }
            else
            {
                cell = cell.Offset(0, 0, step.Z);
                entry = from + dir * (float)tMaxZ;
                tMaxZ += tDeltaZ;
            }
        }
    }

    /// <summary>
    /// 锥体遍历：从 origin 沿 dir 打出最长 maxDistance 的直线，直线随距离线性
    /// 加粗成锥体（起点半径 0、末端半径 radiusAtDistance），产出锥体内所有格
    /// 及其投影点（格中心到直线轴的最近点，世界 cm），由近及远。
    /// <paramref name="dir"/> 任意长度——内部自动归一化；零向量返回空序列。
    /// </summary>
    /// <remarks>
    /// - 逐格中心（格索引 × 格长）做点线投影（<see cref="Visibility.Project"/>），
    ///   投影距离在 (0, maxDistance] 且垂距 ≤ 投影距离 / maxDistance × radiusAtDistance
    ///   （半径随距离线性扩大，起点处为 0）才覆盖；覆盖点取投影点。
    /// - 包围盒取"起终点格 ± 末端半径"，保证锥体内所有格都会被检查
    ///   （成本随末端半径三次方增长）。
    /// - 无法惰性早停：包围盒枚举顺序与距离无关，须先全量收集并按距离排序才能
    ///   由近及远返回——调用方只取第一个结果时仍会扫描整个包围盒并排序
    ///   （与 <see cref="OnLine"/> 的惰性 DDA 不同；锥体用于"预估可见"，通常消费全部结果）。
    /// - 起点格不排除（调用方按需跳过，如 <c>FindItemsInView</c>）。
    /// </remarks>
    public static IEnumerable<(Int3 Cell, Float3 Point)> InCone(
        Float3 origin,
        Float3 dir,
        float maxDistance,
        float radiusAtDistance,
        float length,
        Func<Float3, Int3> snap)
    {
        var mag = dir.Magnitude;
        if (mag < 1e-6f)
            return Array.Empty<(Int3 Cell, Float3 Point)>();
        var d = dir / mag;

        var pad = (int)Math.Ceiling(radiusAtDistance / length);
        var min = snap(origin) - new Int3(pad, pad, pad);
        var max = snap(origin + d * maxDistance) + new Int3(pad, pad, pad);

        // 包围盒内逐格中心投影：垂距 ≤ 投影距离 / maxDistance × 末端半径
        var cells = new List<(Int3 Cell, Float3 Point)>();
        foreach (var c in Int3.Range(min, max))
        {
            var p = Visibility.Project(new Float3(c.X * length, c.Y * length, c.Z * length), origin, d);
            if (p.Distance <= 0 || p.Distance > maxDistance)
                continue;
            if (p.LateralDistance > p.Distance / maxDistance * radiusAtDistance)
                continue;
            cells.Add((c, origin + d * p.Distance));
        }
        return cells.OrderBy(c => (c.Point - origin).Magnitude); // 由近及远
    }

    /// <summary>
    /// 视锥遍历：从 origin 沿 dir 打出最长 maxDistance 的直线，横截面为矩形
    /// （宽 = halfWidthAtDistance × 2、高 = halfHeightAtDistance × 2，随距离线性
    /// 扩大，起点处为 0），产出视锥体内所有格及其投影点（格中心到直线轴的
    /// 最近点，世界 cm），由近及远。视野的"上方向"由 <paramref name="up"/>
    /// 指定（与 dir 不平行的任意向量，如世界 +Y）。
    /// <paramref name="dir"/> 任意长度——内部自动归一化；零向量或
    /// <paramref name="up"/> 与 dir 平行（无法确定宽 / 高方向）时返回空序列。
    /// </summary>
    /// <remarks>
    /// - 逐格中心（格索引 × 格长）做点线投影（<see cref="Visibility.Project"/>），
    ///   投影距离在 (0, maxDistance] 且横向分量分解到"右 / 上"两轴（由 up 与 dir
    ///   构造正交基）后，|右分量| ≤ halfWidthAtDistance × t/maxDistance 且
    ///   |上分量| ≤ halfHeightAtDistance × t/maxDistance 才覆盖；覆盖点取投影点。
    /// - 包围盒取"起终点格 ± 末端半宽半高较大者"，保证视锥体内所有格都会被检查
    ///   （成本随末端尺寸三次方增长）。
    /// - 无法惰性早停：与 <see cref="InCone"/> 相同，须先全量收集并按距离排序
    ///   才能由近及远返回。
    /// - 起点格不排除（调用方按需跳过，如 <c>FindItemsInView</c>）。
    /// </remarks>
    public static IEnumerable<(Int3 Cell, Float3 Point)> InFrustum(
        Float3 origin,
        Float3 dir,
        Float3 up,
        float maxDistance,
        float halfWidthAtDistance,
        float halfHeightAtDistance,
        float length,
        Func<Float3, Int3> snap)
    {
        var mag = dir.Magnitude;
        if (mag < 1e-6f)
            return Array.Empty<(Int3 Cell, Float3 Point)>();
        var d = dir / mag;

        // 正交基：right = 归一化(dir × up)，upV = right × dir（与 right、d 正交且已单位）
        var right = Float3.Cross(d, up);
        var rightMag = right.Magnitude;
        if (rightMag < 1e-6f)
            return Array.Empty<(Int3 Cell, Float3 Point)>();
        right = right / rightMag;
        var upV = Float3.Cross(right, d);

        var pad = (int)Math.Ceiling(Math.Max(halfWidthAtDistance, halfHeightAtDistance) / length);
        var min = snap(origin) - new Int3(pad, pad, pad);
        var max = snap(origin + d * maxDistance) + new Int3(pad, pad, pad);

        // 包围盒内逐格中心投影：横向分量分解到右 / 上轴，随距离线性限宽限高
        var cells = new List<(Int3 Cell, Float3 Point)>();
        foreach (var c in Int3.Range(min, max))
        {
            var p = Visibility.Project(new Float3(c.X * length, c.Y * length, c.Z * length), origin, d);
            if (p.Distance <= 0 || p.Distance > maxDistance)
                continue;
            var scale = p.Distance / maxDistance;
            if (Math.Abs(Float3.Dot(p.Lateral, right)) > halfWidthAtDistance * scale)
                continue;
            if (Math.Abs(Float3.Dot(p.Lateral, upV)) > halfHeightAtDistance * scale)
                continue;
            cells.Add((c, origin + d * p.Distance));
        }
        return cells.OrderBy(c => (c.Point - origin).Magnitude); // 由近及远
    }
}
