namespace Momoka.Core.Events;

/// <summary>
/// 监听优先级：高者先执行，同级按注册先后（<see cref="Lowest"/> 即常规档位中最晚执行）。
/// </summary>
public enum EventPriority
{
    /// <summary>最低。</summary>
    Lowest = 0,

    /// <summary>低。</summary>
    Low = 1,

    /// <summary>普通（默认）。</summary>
    Normal = 2,

    /// <summary>高。</summary>
    High = 3,

    /// <summary>最高。</summary>
    Highest = 4,
}
