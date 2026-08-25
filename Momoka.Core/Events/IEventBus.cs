namespace Momoka.Core.Events;

/// <summary>
/// 强类型事件总线：订阅表按事件类型分桶，发布时快照订阅表再分发；
/// 事件类型由插件自声明，Core 不定义业务事件。
/// </summary>
public interface IEventBus
{
    /// <summary>订阅事件；返回的令牌用于退订（幂等）。</summary>
    IDisposable Subscribe<TEvent>(Func<TEvent, Task> handler);

    /// <summary>发布事件（按分发模式执行订阅者）。</summary>
    Task PublishAsync<TEvent>(
        TEvent @event,
        DispatchMode mode = DispatchMode.Sequential,
        CancellationToken cancellationToken = default);
}
