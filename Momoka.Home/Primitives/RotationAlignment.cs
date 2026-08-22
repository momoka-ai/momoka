namespace Momoka.Home.Primitives;

/// <summary>
/// 放置表面朝向类别——物件声明的"期望表面类型"（模板配置
/// <c>rotation_alignment</c> 属性，**缺省 <see cref="Upside"/>**——未配置的
/// 物件只可放到朝上的水平表面，符合物理学直觉），
/// 决定物件可被放置到什么朝向的表面上；校验见
/// <see cref="RotationAlignmentExtensions.Matches(RotationAlignment, RotationAlignment)"/> 与
/// <c>LevelLayout.Add(Entity, Position, PlacementLayoutSource)</c>。
/// 与 <see cref="Rotation.Alignment"/>（表面朝向的分类）配套使用。
/// </summary>
/// <remarks>
/// 粒度说明：类别只区分"表面类型"——垂直面不分东西南北、斜面不分角度；
/// 方向 / 角度属于物件自身的姿态约束（放置时的 <see cref="Rotation"/>），
/// 不在表面类别中表达。
/// </remarks>
public enum RotationAlignment
{
    /// <summary>朝上的水平表面（法向 +Y：地板 / 桌面顶面）。缺省值（0）——未配置即此。</summary>
    Upside = 0,
    /// <summary>水平表面（法向沿 Y，不区分上下——地毯等）。匹配 Upside / Downside。</summary>
    Horizontal,
    /// <summary>朝下的水平表面（法向 −Y：天花板底面——吸顶灯）。</summary>
    Downside,
    /// <summary>垂直表面（法向水平：墙面——挂画；不分东西南北）。</summary>
    Vertical,
    /// <summary>倾斜表面（法向斜向：屋顶坡面——太阳能板；不分角度）。</summary>
    Tilted,
}

/// <summary>
/// <see cref="RotationAlignment"/> 的匹配规则：期望类别与实际表面类别是否相容。
/// 精确匹配之外，仅 <see cref="RotationAlignment.Horizontal"/> 额外接受
/// Upside / Downside 两种水平面（"水平"不区分上下）。
/// </summary>
public static class RotationAlignmentExtensions
{
    /// <summary>期望类别是否接受实际表面类别（<c>LevelLayout.Add</c> 放置校验使用）。</summary>
    public static bool Matches(this RotationAlignment required, RotationAlignment actual) =>
        required == actual
        || (required == RotationAlignment.Horizontal && actual is RotationAlignment.Upside or RotationAlignment.Downside);
}
