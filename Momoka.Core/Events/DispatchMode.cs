namespace Momoka.Core.Events;

/// <summary>事件分发模式。</summary>
public enum DispatchMode
{
    /// <summary>按订阅顺序依次 await；单个 handler 异常隔离并记录。</summary>
    Sequential = 0,

    /// <summary><c>Task.WhenAll</c> 并行分发；异常聚合记录。</summary>
    Parallel = 1,

    /// <summary>后台 fire-and-forget 分发（handler 异常各自隔离）。</summary>
    Background = 2,
}
