namespace Momoka.Home.Primitives;

/// <summary>
/// 放置表面朝向类别——物件声明的"期望表面类型"（模板配置
/// <c>direction_alignment</c> 属性，缺省 <see cref="Any"/>），
/// 决定物件可被放置到什么朝向的表面上；校验见
/// <c>UnitLayout.Add(Entity, Position, Entity)</c>。
/// 与 <see cref="Rotation.Alignment"/>（表面朝向的分类）配套使用。
/// </summary>
public enum DirectionAlignment
{
    /// <summary>任意朝向的表面（缺省：普通物件不限定表面）。</summary>
    Any = 0,
    /// <summary>水平表面（法向沿 Y，不区分上下）。</summary>
    Horizontal,
    /// <summary>朝上的水平表面（法向 +Y：地板 / 桌面顶面）。</summary>
    Upside,
    /// <summary>朝下的水平表面（法向 −Y：天花板底面——吸顶灯）。</summary>
    Downside,
    /// <summary>垂直表面（法向水平：墙面——挂画）。</summary>
    Vertical,
    /// <summary>倾斜表面（法向斜向：屋顶坡面——太阳能板）。</summary>
    Tilted,
}
