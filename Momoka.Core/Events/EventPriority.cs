namespace Momoka.Core.Events;

/// <summary>
/// 监听优先级：高者先执行，同级按注册先后（<see cref="Lowest"/> 即常规档位中最晚执行）。
/// </summary>
public enum EventPriority
{
    Lowest = 0,
    Low = 1,
    Normal = 2,
    High = 3,
    Highest = 4,
}
