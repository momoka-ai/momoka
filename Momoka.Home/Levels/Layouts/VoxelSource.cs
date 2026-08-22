using System.Numerics;
using Momoka.Home.Levels;
using Momoka.Home.Levels.Volumes;
using Momoka.Home.Primitives;
using Momoka.Home.Levels.Entities;

using Momoka.Home.Levels.Entities.Properties;
namespace Momoka.Home.Levels.Layouts;

/// <summary>
/// 体素空间查询源：向空间查询扩展方法（视线 / 射线 / 碰撞 / 寻路）暴露底层体素网格。
/// 实现者只需提供一个 <see cref="VoxelLayout{T}"/>，即可获得全套空间查询能力
/// （如 <see cref="LevelLayout"/> 即同时实现 <c>IEntitySource</c> 与 <c>IVoxelSource&lt;Entity&gt;</c>）。
/// </summary>
/// <typeparam name="T">体素格中存储的值类型（如 <c>Entity</c>、<c>Region</c>），须为非空类型。</typeparam>
public interface IVoxelSource<T> where T : notnull
{
    /// <summary>底层体素网格：10cm 一格，<see cref="Int3"/> 为格寻址原语，<see cref="Position"/> 为自描述坐标。</summary>
    VoxelLayout<T> Voxels { get; }
}

/// <summary>
/// <see cref="IVoxelSource{T}"/> 的空间查询扩展：基于体素网格提供四类查询——
/// 视线（<c>CanSee</c>）、直线 / 圆锥 / 视锥内目标（<c>FindItemsOnLine</c> /
/// <c>FindItemsInCone</c> / <c>FindItemsInView</c>）、碰撞（<c>IsCollided</c>）、
/// 寻路（<c>FindPath</c>）。所有坐标以世界 cm 为准，内部自动对齐到 10cm 体素格寻址。
/// </summary>
public static class VoxelSourceExtensions
{
    /// <summary>
    /// 两点间遮挡判定：沿 src → dest 的射线逐格穿越（<see cref="Traverse.OnLine"/>），
    /// 起点之后、终点之前存在按 <paramref name="occlusion"/> 档位判定为阻挡的实体
    /// （<see cref="OcclusionExtensions.Blocks{T}"/>，即墙 / 门 / 窗等固定建筑结构），
    /// 则为被遮挡。空格（未写入或 Bound 外）不阻挡——见 <see cref="OcclusionExtensions.Blocks{T}"/>。
    /// </summary>
    /// <remarks>
    /// - 起点格（Skip(1)）与终点格（TakeWhile 止于终点前）均不参与判定——
    ///   "看向一堵墙"不算被遮挡，因为终点格正是墙本身所在的格。
    /// - <paramref name="exclude"/>：从判定中排除的实体（其占据的任何格都不算遮挡）。
    ///   命中扫描中对候选实体调用本方法时传候选自身（h.Hit）——投影点（格中心
    ///   垂足）在斜射时可能落邻格，终点格排除不足以避开实体自身的格，须显式排除。
    /// - 严格逐点语义：只有 src → dest 连线上存在阻挡实体才算遮挡。
    ///   圆锥等体积扫描中对每个候选实体调用本方法，即"逐实体射线遮挡判定"。
    /// - <see cref="Occlusion.None"/> 短路：该档位下无任何实体阻挡，恒返回 false。
    /// - 默认 <see cref="Occlusion.OnlyImmutable"/>：固定结构阻挡视线——与
    ///   <see cref="CanSee{T}(IVoxelSource{T}, Position, Position)"/> 语义一致，
    ///   后者即本方法在该档位下的取反。
    /// </remarks>
    /// <typeparam name="T">体素格值类型，须支持属性查询（Occlusion 阻挡判定需要）。</typeparam>
    /// <param name="voxelSource">体素空间查询源。</param>
    /// <param name="src">观察点（世界 cm，任意 Position 尺度均可，内部经 Absolute() 归一）。</param>
    /// <param name="dest">目标点（世界 cm）。</param>
    /// <param name="occlusion">阻挡档位：哪些实体算遮挡（见 remarks）。</param>
    /// <param name="exclude">排除的实体（其占格不参与判定）；命中扫描传目标实体自身，默认不排除。</param>
    /// <returns>起点与终点之间存在阻挡实体时为 <c>true</c>，否则 <c>false</c>。</returns>
    public static bool IsOccluded<T>(
        this IVoxelSource<T> voxelSource,
        Position src,
        Position dest,
        Occlusion occlusion = Occlusion.OnlyImmutable,
        T? exclude = default)
        where T : IPropertySource
    {
        if (occlusion == Occlusion.None)
            return false; // None 档位：无任何实体阻挡，遮挡恒为 false——短路，省去射线遍历
        var voxels = voxelSource.Voxels;
        var from = src.Absolute();
        var to = dest.Absolute();
        var destCell = voxels.GetAsRelative(to);
        return Traverse.OnLine(from, to, voxels.Length, voxels.GetAsRelative)
            .Skip(1)
            .TakeWhile(c => c.Cell != destCell)
            .Any(c => voxels[c.Cell] is { } v && !v.Equals(exclude) && occlusion.Blocks(v));
    }

    /// <summary>
    /// 两点间视线判定：起点与终点之间无按 <see cref="Occlusion.OnlyImmutable"/> 档位
    /// 阻挡的实体（墙 / 门 / 窗等固定结构）即为可见——委托
    /// <see cref="IsOccluded{T}(IVoxelSource{T}, Position, Position, Occlusion, T)"/> 取反。
    /// </summary>
    /// <remarks>
    /// 纯二值遮挡判定：不含锥形视野 / 距离限制（见带 <c>direction</c> 参数的锥形重载）。
    /// </remarks>
    /// <example>
    /// <code>
    /// // 人站在原点，灯在 (50, 0, 50)，中间隔一堵墙 → false
    /// bool visible = unit.CanSee(new Position(0, 0, 0), new Position(50, 0, 50));
    /// </code>
    /// </example>
    /// <typeparam name="T">体素格值类型，须支持属性查询（以 <see cref="Property.IsImmutable"/> 判穿透）。</typeparam>
    /// <param name="voxelSource">体素空间查询源。</param>
    /// <param name="src">观察点（世界 cm，任意 Position 尺度均可，内部经 Absolute() 归一）。</param>
    /// <param name="dest">目标点（世界 cm）。</param>
    /// <returns>起点与终点之间无不可穿透实体时为 <c>true</c>，否则 <c>false</c>。</returns>
    public static bool CanSee<T>(
        this IVoxelSource<T> voxelSource,
        Position src,
        Position dest)
        where T : IPropertySource =>
        !voxelSource.IsOccluded(src, dest, Occlusion.OnlyImmutable);

    /// <summary>
    /// 对目标包围盒的视线判定：取观察点到包围盒上最近的点作为目标点，
    /// 再委托两点 <see cref="CanSee{T}(IVoxelSource{T}, Position, Position)"/> 做遮挡判定。
    /// </summary>
    /// <remarks>
    /// 用于"能否看到某个有体积的物体"：以盒上最近点代替单点目标，
    /// 避免目标点落在物体内部 / 被物体自身遮挡而误判为不可见。
    /// </remarks>
    /// <example>
    /// <code>
    /// // 人看向 60×120×60 的柜子（包围盒）
    /// bool visible = unit.CanSee(eyePos, new Bound(0, 0, 0, 60, 120, 60));
    /// </code>
    /// </example>
    /// <typeparam name="T">体素格值类型，须支持属性查询。</typeparam>
    /// <param name="voxelSource">体素空间查询源。</param>
    /// <param name="src">观察点（世界 cm）。</param>
    /// <param name="dest">目标包围盒（世界 cm，如家具的 <see cref="Bound"/>）。</param>
    /// <returns>观察点到盒上最近点之间无不可穿透实体时为 <c>true</c>。</returns>
    public static bool CanSee<T>(
        this IVoxelSource<T> voxelSource,
        Position src,
        Bound dest)
        where T : IPropertySource
    {
        var s = src.Absolute();
        var target = new Float3(
            Math.Clamp(s.X, dest.Min.X, dest.Max.X),
            Math.Clamp(s.Y, dest.Min.Y, dest.Max.Y),
            Math.Clamp(s.Z, dest.Min.Z, dest.Max.Z));
        return voxelSource.CanSee(src, new Position(target));
    }

    /// <summary>
    /// 锥形视野内视线判定：先判断 dest 是否落在以 src 为顶点、direction 为轴、
    /// maxDistance 为射程、末端半径 maxRadius 的锥体内（<see cref="Visibility.IsInCone"/>，
    /// 半径随距离线性扩大——近端锥窄），
    /// 再委托两点 <see cref="CanSee{T}(IVoxelSource{T}, Position, Position)"/> 做遮挡判定。
    /// 两步都通过才可见。
    /// </summary>
    /// <remarks>
    /// <paramref name="direction"/> 任意长度——内部自动归一化；零向量直接返回 false。
    /// 这是"传感器 / 摄像头视野 + 遮挡"的完整查询，纯几何包含 + 二值遮挡。
    /// </remarks>
    /// <example>
    /// <code>
    /// // 摄像头在 (0,0,0) 朝 +X，射程 5m、锥半径 50cm，判断目标是否在视野内且未被遮挡
    /// bool visible = unit.CanSee(
    ///     new Position(0, 0, 0), new Position(300, 100, 20), Vector3.UnitX, 500, 50);
    /// </code>
    /// </example>
    /// <typeparam name="T">体素格值类型，须支持属性查询。</typeparam>
    /// <param name="voxelSource">体素空间查询源。</param>
    /// <param name="src">观察点（世界 cm）。</param>
    /// <param name="dest">目标点（世界 cm）。</param>
    /// <param name="direction">视线方向（任意长度，内部归一化；零向量返回 false）。</param>
    /// <param name="maxDistance">最大射程（cm），目标投影距离超过即不可见。</param>
    /// <param name="maxRadius">锥体末端半径（cm）——半径随距离线性扩大（起点处 0，
    /// 末端 maxRadius），目标垂距 > 投影距离 / maxDistance × maxRadius 即不可见。</param>
    /// <returns>目标在锥体内且无遮挡时为 <c>true</c>。</returns>
    public static bool CanSee<T>(
        this IVoxelSource<T> voxelSource,
        Position src,
        Position dest,
        Vector3 direction,
        float maxDistance,
        float maxRadius) where T : IPropertySource
    {
        var dir = new Float3(direction.X, direction.Y, direction.Z);
        var mag = dir.Magnitude;
        if (mag < 1e-6f)
            return false;
        dir = dir / mag;
        if (!Visibility.IsInCone(dest.Absolute(), src.Absolute(), dir, maxDistance, maxRadius))
            return false;
        return voxelSource.CanSee(src, dest);
    }

    /// <summary>
    /// 视野内目标（射线版）：从 src 沿方向 dir 打出一条最长 distance cm 的直线，
    /// 返回这条线触碰到的所有实体（含精确命中点），从近到远逐个产出
    /// （惰性求值，调用方提前停止时扫描随之停止），同一实体只保留最近一次命中；
    /// 按 <see cref="Occlusion"/> 阻挡档位在首个阻挡实体处停止
    /// （默认 <see cref="Occlusion.None"/>：穿透一切，返回全部）。
    /// </summary>
    /// <remarks>
    /// - <paramref name="dir"/> 任意长度——内部自动归一化；零向量返回空序列。
    /// - 命中点取直线进入格面的精确交点（<see cref="Traverse.OnLine"/> 走廊遍历，覆盖直线穿过的格）。
    /// - 惰性 + 早停：DDA 顺序天然由近及远，逐格产出；调用方只取前几个命中
    ///   （如 <c>.FirstOrDefault()</c>）或遇到阻挡实体时，扫描提前停止，不会遍历整条线。
    /// - Occlusion 是"阻挡档位"：被阻挡的实体返回后扫描即停止，之前的实体照常返回；
    ///   None = 任何实体都不阻挡（返回沿线全部）、
    ///   OnlyImmutable = 固定结构阻挡（可变实体被穿透并返回，第一面墙处停止）、
    ///   OnlyNonTransparent = 不透明实体阻挡（透明实体被穿透并返回，第一个不透明实体处
    ///   停止，相当于渲染中的遮挡剔除）、
    ///   Everything = 任何实体都阻挡（只返回最近的一个）。
    /// </remarks>
    /// <example>
    /// <code>
    /// // 沿 +X 打一条 10m 的线，找第一个撞到的固定结构（可变物件会被穿透并返回）
    /// var wall = unit.FindItemsOnLine(new Position(0, 0, 0), Vector3.UnitX, 1000)
    ///     .FirstOrDefault(r => r.Hit.IsImmutable());
    /// </code>
    /// </example>
    /// <typeparam name="T">体素格值类型，须支持属性查询（Occlusion 阻挡判定需要）。</typeparam>
    /// <param name="voxelSource">体素空间查询源。</param>
    /// <param name="src">直线起点（世界 cm）。</param>
    /// <param name="dir">直线方向（任意长度，内部归一化；零向量返回空序列）。</param>
    /// <param name="distance">直线最大长度（cm）；超出网格 Bound 的格视为空，自动停止。</param>
    /// <param name="occlusion">阻挡档位：哪些实体被返回后扫描即停止（见 remarks）。</param>
    /// <returns>触碰到的 <see cref="Collision.Result{T}"/> 序列（由近及远、按实体去重）；无命中时为空序列。</returns>
    public static IEnumerable<Collision.Result<T>> FindItemsOnLine<T>(
        this IVoxelSource<T> voxelSource,
        Position src,
        Vector3 dir,
        float distance,
        Occlusion occlusion = Occlusion.None) where T : IPropertySource
    {
        var voxels = voxelSource.Voxels;
        var s = src.Absolute();
        var d = new Float3(dir.X, dir.Y, dir.Z);
        var mag = d.Magnitude;
        if (mag < 1e-6f)
            yield break;
        d = d / mag;

        var to = s + d * distance;
        var startCell = voxels.GetAsRelative(s);
        var seen = new HashSet<T>(); // 实体级去重：同一实体占多格只返回一次

        // 直线遍历（惰性）：DDA 顺序天然由近及远，逐格产出，可提前停止
        foreach (var (cell, entry) in Traverse.OnLine(s, to, voxels.Length, voxels.GetAsRelative))
        {
            if (cell == startCell)
                continue;
            var value = voxels[cell];
            if (value is null || !seen.Add(value))
                continue;
            yield return new Collision.Result<T>(value, cell, new Position(entry));
            if (occlusion.Blocks(value))
                yield break; // 阻挡档位：遇阻挡实体即停，不再继续扫描
        }
    }

    /// <summary>
    /// 视野内目标（圆锥版）：从 src 沿方向 dir 打出一条最长 distance cm 的直线，
    /// 直线随距离线性加粗成圆锥（起点半径 0，末端半径 coneRadiusAtDistance），
    /// 返回圆锥体内触碰到的所有实体（含投影点），由近及远排列、按实体去重；
    /// 被更近的阻挡实体遮挡者跳过（<see cref="Occlusion"/> 阻挡档位，
    /// 默认 <see cref="Occlusion.None"/>：无阻挡，返回全部）。
    /// </summary>
    /// <remarks>
    /// - <paramref name="dir"/> 任意长度——内部自动归一化；零向量返回空序列。
    /// - 遍历与判定见 <see cref="Traverse.InCone"/>（包围盒 + <see cref="Visibility.Project"/>
    ///   逐格投影判定 + 由近及远排序，成本随末端半径三次方增长、无法惰性早停）。
    /// - 遮挡为严格语义：对每个候选实体做 src → 命中点的射线判定
    ///   （<see cref="Traverse.OnLine"/>），直线上存在更近的阻挡实体
    ///   （<see cref="Occlusion"/> 档位判定）即视为被遮挡、跳过——而非"按距离
    ///   截断"（射线重载的 break 模式只在 1D 下等价于遮挡；圆锥是体积扫描，
    ///   距离更远的侧向实体未必被遮挡，距离截断会误杀未被遮挡的实体）。
    /// - 其它形状变体（长方形视锥等）以后续重载加入，基于
    ///   <see cref="Visibility.Project"/> 的分解做形状判定。
    /// </remarks>
    /// <example>
    /// <code>
    /// // 末端半径 50cm 的圆锥（模拟人眼视野），任何实体都阻挡 → 只取最近被撞到的实体
    /// var nearest = unit.FindItemsInCone(eyePos, aimDir, 500, 50, Occlusion.Everything)
    ///     .FirstOrDefault();
    /// </code>
    /// </example>
    /// <typeparam name="T">体素格值类型，须支持属性查询（Occlusion 阻挡判定需要）。</typeparam>
    /// <param name="voxelSource">体素空间查询源。</param>
    /// <param name="src">直线起点（世界 cm）。</param>
    /// <param name="dir">直线方向（任意长度，内部归一化；零向量返回空序列）。</param>
    /// <param name="distance">直线最大长度（cm）。</param>
    /// <param name="coneRadiusAtDistance">圆锥末端（distance 处）的半径（cm）；起点处为 0，随距离线性扩大。</param>
    /// <param name="occlusion">阻挡档位：哪些实体被返回后扫描即停止（见 remarks）。</param>
    /// <returns>触碰到的 <see cref="Collision.Result{T}"/> 序列（由近及远、按实体去重）；无命中时为空序列。</returns>
    public static IEnumerable<Collision.Result<T>> FindItemsInCone<T>(
        this IVoxelSource<T> voxelSource,
        Position src,
        Vector3 dir,
        float distance,
        float coneRadiusAtDistance,
        Occlusion occlusion = Occlusion.None) where T : IPropertySource
    {
        var voxels = voxelSource.Voxels;
        var s = src.Absolute();
        var d = new Float3(dir.X, dir.Y, dir.Z);
        var mag = d.Magnitude;
        if (mag < 1e-6f)
            return Array.Empty<Collision.Result<T>>();
        d = d / mag;

        // 锥体遍历（见 Traverse.InCone）：包围盒扫描 + 投影判定 + 由近及远排序
        return FilteredHits(voxelSource, src, voxels.GetAsRelative(s),
            Traverse.InCone(s, d, distance, coneRadiusAtDistance, voxels.Length, voxels.GetAsRelative),
            occlusion);
    }

    /// <summary>
    /// 视野内目标（视锥版）：从 src 沿方向 dir 打出最长 distance cm 的直线，
    /// 横截面为矩形（宽 = halfWidthAtDistance × 2、高 = halfHeightAtDistance × 2，
    /// 随距离线性扩大，起点处为 0），返回视锥体内触碰到的所有实体（含投影点），
    /// 由近及远排列、按实体去重；被更近的阻挡实体遮挡者跳过
    /// （<see cref="Occlusion"/> 阻挡档位，默认 <see cref="Occlusion.None"/>：无阻挡，返回全部）。
    /// </summary>
    /// <remarks>
    /// - <paramref name="dir"/> 任意长度——内部自动归一化；零向量返回空序列。
    /// - <paramref name="up"/> 为视野"上方向"（与 dir 不平行的任意向量，如世界 +Y）；
    ///   与 dir 平行（无法确定宽 / 高方向）时返回空序列。
    /// - 遍历与判定见 <see cref="Traverse.InFrustum"/>（包围盒 + <see cref="Visibility.Project"/>
    ///   逐格投影判定 + 由近及远排序，成本随末端尺寸三次方增长、无法惰性早停）。
    /// - 遮挡为严格语义，同圆锥重载：对每个候选实体做 src → 命中点的射线判定
    ///   （<see cref="IsOccluded{T}(IVoxelSource{T}, Position, Position, Occlusion, T)"/>，
    ///   排除候选自身），被更近的阻挡实体遮挡者跳过。
    /// </remarks>
    /// <example>
    /// <code>
    /// // 模拟 4:3 摄像头画面（射程 5m，末端半宽 60cm、半高 45cm），任何实体都阻挡 → 只取最近被撞到的实体
    /// var nearest = unit.FindItemsInView(camPos, camDir, Vector3.UnitY, 500, 60, 45, Occlusion.Everything)
    ///     .FirstOrDefault();
    /// </code>
    /// </example>
    /// <typeparam name="T">体素格值类型，须支持属性查询（Occlusion 阻挡判定需要）。</typeparam>
    /// <param name="voxelSource">体素空间查询源。</param>
    /// <param name="src">直线起点（世界 cm）。</param>
    /// <param name="dir">直线方向（任意长度，内部归一化；零向量返回空序列）。</param>
    /// <param name="up">视野上方向（世界坐标，与 dir 不平行的任意向量）。</param>
    /// <param name="distance">直线最大长度（cm）。</param>
    /// <param name="halfWidthAtDistance">视锥末端（distance 处）的半宽（cm）；起点处为 0，随距离线性扩大。</param>
    /// <param name="halfHeightAtDistance">视锥末端（distance 处）的半高（cm）；起点处为 0，随距离线性扩大。</param>
    /// <param name="occlusion">阻挡档位：哪些实体被返回后扫描即停止（见 remarks）。</param>
    /// <returns>触碰到的 <see cref="Collision.Result{T}"/> 序列（由近及远、按实体去重）；无命中时为空序列。</returns>
    public static IEnumerable<Collision.Result<T>> FindItemsInView<T>(
        this IVoxelSource<T> voxelSource,
        Position src,
        Vector3 dir,
        Vector3 up,
        float distance,
        float halfWidthAtDistance,
        float halfHeightAtDistance,
        Occlusion occlusion = Occlusion.None) where T : IPropertySource
    {
        var voxels = voxelSource.Voxels;
        var s = src.Absolute();
        var d = new Float3(dir.X, dir.Y, dir.Z);
        var mag = d.Magnitude;
        if (mag < 1e-6f)
            return Array.Empty<Collision.Result<T>>();
        d = d / mag;

        // 视锥遍历（见 Traverse.InFrustum）：包围盒扫描 + 判定 + 由近及远排序
        return FilteredHits(voxelSource, src, voxels.GetAsRelative(s),
            Traverse.InFrustum(s, d, new Float3(up.X, up.Y, up.Z), distance,
                halfWidthAtDistance, halfHeightAtDistance, voxels.Length, voxels.GetAsRelative),
            occlusion);
    }

    /// <summary>
    /// 形状遍历 → 命中实体的组装（圆锥 / 视锥重载共用）：排除起点格、取格值，
    /// 由近及远按实体去重后，逐实体做射线遮挡判定（严格语义，见
    /// <see cref="IsOccluded{T}(IVoxelSource{T}, Position, Position, Occlusion, T)"/>，
    /// 排除候选自身）——被更近的阻挡实体遮挡者跳过。
    /// 体积扫描不能用射线重载的"首阻挡截断"：距离更远的侧向实体未必被遮挡，
    /// 按距离一刀切会误杀未被遮挡的实体。
    /// </summary>
    private static List<Collision.Result<T>> FilteredHits<T>(
        IVoxelSource<T> voxelSource,
        Position src,
        Int3 startCell,
        IEnumerable<(Int3 Cell, Float3 Point)> cells,
        Occlusion occlusion) where T : IPropertySource
    {
        var voxels = voxelSource.Voxels;
        var hits = new List<Collision.Result<T>>();
        foreach (var (cell, point) in cells)
        {
            if (cell == startCell)
                continue;
            var value = voxels[cell];
            if (value is null)
                continue;
            hits.Add(new Collision.Result<T>(value, cell, new Position(point)));
        }
        return hits.DistinctBy(h => h.Hit)
            .Where(h => !voxelSource.IsOccluded(src, h.Point, occlusion, h.Hit))
            .ToList();
    }

    /// <summary>
    /// 范围内实体（占用格语义）：返回占用格 XZ 落在 [min, max]（含）内的所有实体，
    /// 按实体去重。逐列经 <see cref="VoxelIterator{T}"/> 从 Bound 底扫到顶（惰性 yield，
    /// 消费前不执行）。与 <see cref="EntitySourceExtensions.FindEntitiesInBound(IEntitySource, Bound)"/>
    /// 的"锚点在范围内"语义不同：本方法按占用格判定（拖拽选择等场景）。
    /// </summary>
    /// <example>
    /// <code>
    /// // 选中 XZ 格范围 [0,0]–[3,3] 内的所有家具
    /// var selected = unit.FindItemsInBound(new Int2(0, 0), new Int2(3, 3));
    /// </code>
    /// </example>
    /// <typeparam name="T">体素格值类型。</typeparam>
    /// <param name="voxelSource">体素空间查询源。</param>
    /// <param name="min">范围最小格（含）。</param>
    /// <param name="max">范围最大格（含）。</param>
    /// <returns>占用格在范围内的实体序列（去重）；网格未设置 Bound 时为空序列。</returns>
    public static IEnumerable<T> FindItemsInBound<T>(
        this IVoxelSource<T> voxelSource,
        Int2 min,
        Int2 max) where T : notnull
    {
        var voxels = voxelSource.Voxels;
        var seen = new HashSet<T>();
        for (var x = min.X; x <= max.X; x++)
            for (var z = min.Z; z <= max.Z; z++)
                foreach (var (_, value) in voxels.GetIteratorAt(x, z))
                    if (value is not null && seen.Add(value))
                        yield return value;
    }

    /// <summary>
    /// 点碰撞查询：src 所在格是否被实体占据，返回占据该格的实体命中信息。
    /// </summary>
    /// <example>
    /// <code>
    /// // 判断 (35, 10, 42) 处是否有家具
    /// var hit = unit.IsCollided(new Position(35, 10, 42));
    /// </code>
    /// </example>
    /// <typeparam name="T">体素格值类型。</typeparam>
    /// <param name="voxelSource">体素空间查询源。</param>
    /// <param name="src">查询点（世界 cm）。</param>
    /// <returns>占据该格的 <see cref="Collision.Result{T}"/>；空格返回 null。</returns>
    public static Collision.Result<T>? IsCollided<T>(
        this IVoxelSource<T> voxelSource,
        Position src) where T : notnull
    {
        var voxels = voxelSource.Voxels;
        var cell = voxels.GetAsRelative(src.Absolute());
        var value = voxels[cell];
        return value is null
            ? null
            : new Collision.Result<T>(value, cell, new Position(voxels.GetAsAbsolute(cell)));
    }

    /// <summary>
    /// 球体碰撞查询：以 src 为球心、radius 为半径的球体内是否存在被占据的格，
    /// 返回枚举顺序上的第一个命中格的实体信息。
    /// </summary>
    /// <remarks>
    /// 按体素粒度判定：扫描以球心格为中心、恰好覆盖球体的立方体范围，
    /// 以"格子中心到球心的距离 ≤ radius"作为命中判据（球体近似，边界格可能漏判/多判）。
    /// </remarks>
    /// <example>
    /// <code>
    /// // 检查半径 30cm 的球形范围内有没有障碍（如机器人原地旋转）
    /// var hit = unit.IsCollided(new Position(120, 0, 80), 30f);
    /// </code>
    /// </example>
    /// <typeparam name="T">体素格值类型。</typeparam>
    /// <param name="voxelSource">体素空间查询源。</param>
    /// <param name="src">球心（世界 cm）。</param>
    /// <param name="radius">球半径（cm）。</param>
    /// <returns>球体内第一个占据格的 <see cref="Collision.Result{T}"/>；为空返回 null。</returns>
    public static Collision.Result<T>? IsCollided<T>(
        this IVoxelSource<T> voxelSource,
        Position src,
        float radius) where T : notnull
    {
        var voxels = voxelSource.Voxels;
        var s = src.Absolute();
        var center = voxels.GetAsRelative(s);
        var r = (int)Math.Ceiling(radius / voxels.Length);
        var r2 = (double)radius * radius;
        foreach (var cell in Int3.Range(center.Offset(-r, -r, -r), center.Offset(r, r, r)))
        {
            var value = voxels[cell];
            if (value is null)
                continue;
            var p = voxels.GetAsAbsolute(cell) - s;
            if (Float3.Dot(p, p) <= r2)
                return new Collision.Result<T>(value, cell, new Position(voxels.GetAsAbsolute(cell)));
        }
        return null;
    }

    /// <summary>
    /// 体积碰撞查询：把 volume 的形状（相对 src 对齐到体素格）栅格化，
    /// 检查其占用的格中是否有被占据的，返回第一个冲突格的实体信息。
    /// </summary>
    /// <remarks>
    /// 用于放置校验：放置前检查新物体的体积与现有空间是否重叠
    /// （<see cref="LevelLayout.Add(Entity)"/> 即以此为前置检查）。
    /// </remarks>
    /// <example>
    /// <code>
    /// // 检查一个 2×2×2 格的箱子放在 (50, 0, 50) 是否与现有实体重叠
    /// var box = new Box { SizeX = 2, SizeY = 2, SizeZ = 2 };
    /// var hit = unit.IsCollidedVolume(new Position(50, 0, 50), box);
    /// </code>
    /// </example>
    /// <typeparam name="T">体素格值类型。</typeparam>
    /// <param name="voxelSource">体素空间查询源。</param>
    /// <param name="src">体积锚点（世界 cm，对应 volume 的局部原点）。</param>
    /// <param name="volume">要检测的体积（<see cref="Volume"/>，经 Cells3D() 提供占格）。</param>
    /// <returns>第一个重叠实体的 <see cref="Collision.Result{T}"/>；无重叠返回 null。</returns>
    public static Collision.Result<T>? IsCollidedVolume<T>(
        this IVoxelSource<T> voxelSource,
        Position src,
        Volume volume) where T : notnull
    {
        var voxels = voxelSource.Voxels;
        var anchor = voxels.GetAsRelative(src.Absolute());
        var hit = volume.Cells3D()
            .Select(offset => anchor + offset)
            .Select(cell => (Cell: cell, Value: voxels[cell]))
            .FirstOrDefault(x => x.Value is not null);
        return hit.Value is null
            ? null
            : new Collision.Result<T>(hit.Value, hit.Cell, new Position(voxels.GetAsAbsolute(hit.Cell)));
    }

    /// <summary>
    /// 寻路（默认预算）：委托带 maxDistance 的重载，预算取 <see cref="Agent.MaxWalkLength"/>
    /// （agent 的默认最大步行距离，cm）。
    /// </summary>
    /// <typeparam name="T">体素格值类型，须支持属性查询（以 IsImmutable 判可行走）。</typeparam>
    /// <param name="voxelSource">体素空间查询源。</param>
    /// <param name="src">起点（世界 cm）。</param>
    /// <param name="dest">终点（世界 cm）。</param>
    /// <param name="agent">移动者参数（身高 / 最大爬升 / 默认步行距离）。</param>
    /// <returns>同带 maxDistance 的重载。</returns>
    public static Pathfinding.Result? FindPath<T>(
        this IVoxelSource<T> voxelSource,
        Position src,
        Position dest,
        Agent agent) where T : IPropertySource =>
        voxelSource.FindPath(src, dest, agent, agent.MaxWalkLength);

    /// <summary>
    /// 寻路：在体素空间上为 agent 规划 src → dest 的可行走路径（A*，XZ 4 连通，
    /// 见 <see cref="Pathfinding.AStar"/>）。只走"可站立"格——格内无实体、向上留有
    /// 身高净高、下方有支撑（地面 / 台阶 / 楼梯等）；允许在 maxClimb 高度内爬升 / 下落，
    /// 爬升每格额外计 0.1 代价。
    /// </summary>
    /// <remarks>
    /// - 起终点先对齐到格，再就近解析到可站立高度；找不到可站立点则退回原高度（可能"悬空"）。
    /// - 终点判定为"XZ 到达目标列且高度差 ≤ maxClimb"，即到达目标附近即可（落点 Y 随接近方向而定）。
    /// - 失败（不可达 / 超预算 / 网格未设置 Bound）统一返回 null——成功路径才是非 null 结果。
    /// - 返回路径为世界 cm 的自描述 Position（scale = 体素长，Absolute() 即 cm）；
    ///   总代价 = 步数 + 爬升惩罚（每爬 1 格 +0.1），非纯几何距离。
    /// </remarks>
    /// <example>
    /// <code>
    /// // 让 1.7m 高的人从 (100,0,100) 走到 (500,0,400)，最大步行 20m
    /// var result = unit.FindPath(
    ///     new Position(100, 0, 100), new Position(500, 0, 400), agent, 2000);
    /// if (result is not null)
    ///     foreach (var waypoint in result.Path) { /* 途经点，Absolute() 为 cm */ }
    /// </code>
    /// </example>
    /// <typeparam name="T">体素格值类型，须支持属性查询（以 IsImmutable 判可行走）。</typeparam>
    /// <param name="voxelSource">体素空间查询源。</param>
    /// <param name="src">起点（世界 cm）。</param>
    /// <param name="dest">终点（世界 cm）。</param>
    /// <param name="agent">移动者参数：Height（身高，净高过滤）、MaxClimbHeight（最大爬升高度）、
    /// MaxWalkLength（默认预算，cm）。</param>
    /// <param name="maxDistance">最大步行距离（cm），超出视为不可达。</param>
    /// <returns>成功：<see cref="Pathfinding.Result"/>（Path 为途经点序列、Distance 为总代价）；
    /// 不可达 / 超预算 / 网格未设置 Bound：null。</returns>
    public static Pathfinding.Result? FindPath<T>(
        this IVoxelSource<T> voxelSource,
        Position src,
        Position dest,
        Agent agent,
        float maxDistance) where T : IPropertySource
    {
        var voxels = voxelSource.Voxels;
        if (!voxels.Bound.Valid)
            return null;

        var length = voxels.Length;
        var height = Math.Max(1, (int)Math.Ceiling(agent.Height / length));
        var maxClimb = (int)Math.Ceiling(agent.MaxClimbHeight / length);

        var startCell = voxels.GetAsRelative(src.Absolute());
        var goalCell = voxels.GetAsRelative(dest.Absolute());
        var min = voxels.GetAsRelative(voxels.Bound.Min);
        var max = voxels.GetAsRelative(voxels.Bound.Max);

        // 可站立判定：格内无实体、上方留有身高净高、下方有支撑（地面/台阶等）
        bool CanStand(int x, int y, int z)
        {
            if (x < min.X || x > max.X || y < min.Y || y > max.Y || z < min.Z || z > max.Z)
                return false;
            if (voxels[new Int3(x, y, z)].IsImmutable())
                return false;
            for (var k = 1; k < height; k++)
                if (voxels[new Int3(x, y + k, z)].IsImmutable())
                    return false;
            return y == min.Y || voxels[new Int3(x, y - 1, z)].IsImmutable();
        }

        // 在 (x, z) 列上从 fromY + maxClimb 向下找最近的可行走 Y；找不到返回 null
        int? StandYAt(int x, int z, int fromY)
        {
            for (var y = fromY + maxClimb; y >= min.Y; y--)
                if (CanStand(x, y, z))
                    return y;
            return null;
        }

        var startY = StandYAt(startCell.X, startCell.Z, startCell.Y) ?? startCell.Y;
        var goalY = StandYAt(goalCell.X, goalCell.Z, goalCell.Y) ?? goalCell.Y;
        var goal = new Int3(goalCell.X, goalY, goalCell.Z);
        var directions = new[] { (1, 0), (-1, 0), (0, 1), (0, -1) };

        return Pathfinding.AStar(
            new Position(startCell with { Y = startY }, length),
            n => n.X == goal.X && n.Z == goal.Z && Math.Abs(n.Y - goal.Y) <= maxClimb,
            n =>
            {
                var nexts = new List<(Int3 Node, double Cost)>(4);
                foreach (var (dx, dz) in directions)
                {
                    var ny = StandYAt(n.X + dx, n.Z + dz, n.Y);
                    if (ny is null)
                        continue;
                    nexts.Add((new Int3(n.X + dx, ny.Value, n.Z + dz),
                        1 + Math.Max(0, ny.Value - n.Y) * 0.1)); // 步 1，爬升 0.1/格
                }
                return nexts;
            },
            n => n.ManhattanDistance(goal),
            maxDistance / length);
    }
}
