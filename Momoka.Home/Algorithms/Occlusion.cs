using Momoka.Home.Entities;
namespace Momoka.Home.Algorithms;

/// <summary>
/// 视野内目标扫描（<c>FindItemsOnLine</c> / <c>FindItemsInCone</c> / <c>FindItemsInView</c>）的阻挡档位：决定哪些实体"阻挡"探测形状。
/// 阻挡的实体被返回后扫描即停止；不阻挡的实体被返回并继续穿透。
/// 判定规则见 <see cref="OcclusionExtensions.Blocks{T}"/>。
/// </summary>
public enum Occlusion
{
    /// <summary>任何实体都不阻挡：穿透一切，从近到远持续返回直线上触碰到的所有实体。</summary>
    None,

    /// <summary>仅带 <see cref="Property.IsImmutable"/> 的固定结构阻挡（墙/门/窗）：
    /// 可变实体（家具等）被穿透并返回，第一个固定结构处停止。</summary>
    OnlyImmutable,

    /// <summary>仅不透明实体阻挡：带 <see cref="Property.IsTransparent"/> 的实体
    /// （玻璃/纱网）被穿透并返回，第一个不透明实体处停止——相当于渲染中的遮挡剔除，
    /// 被不透明实体挡住的后续实体不再返回。</summary>
    OnlyNonTransparent,

    /// <summary>任何实体都阻挡：始终只返回最近的一个实体。</summary>
    Everything,
}

/// <summary><see cref="Occlusion"/> 档位的阻挡判定。</summary>
public static class OcclusionExtensions
{
    /// <summary>按档位判定 <paramref name="value"/> 是否阻挡探测线
    /// （阻挡 = 返回后扫描停止）。<paramref name="value"/> 为 null（空格，如网格
    /// Bound 外或未写入的格）时不阻挡——与射线遍历中"空格跳过"的语义一致，
    /// 空位置不构成遮挡。</summary>
    public static bool Blocks<T>(this Occlusion occlusion, T? value)
        where T : IPropertySource => value is null
        ? false
        : occlusion switch
        {
            Occlusion.None => false,
            Occlusion.OnlyImmutable => value.IsImmutable(),
            Occlusion.OnlyNonTransparent => !value.GetValue<bool>(Property.IsTransparent),
            _ => true,
        };
}
