namespace Momoka.Core.Plugins;

/// <summary>服务解析优先级（枚举值越小越优先）。</summary>
public enum ServicePriority
{
    /// <summary>最高优先。</summary>
    Highest = 0,

    /// <summary>高优先。</summary>
    High = 1,

    /// <summary>普通（默认）。</summary>
    Normal = 2,

    /// <summary>低优先。</summary>
    Low = 3,

    /// <summary>最低优先。</summary>
    Lowest = 4,
}
