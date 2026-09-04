namespace Momoka.Core.Events;

/// <summary>
/// 事件服务（存根）：按事件运行时类型分桶派发（继承现 EventHub 语义并逐步取代它）。
/// Add&lt;T&gt;(Action&lt;T&gt;) 就地封装临时处理器并返回可移除句柄。
/// </summary>
public sealed class EventService
{
    /// <summary>就地封装临时处理器并注册到 T 事件；返回的句柄可交给 <see cref="Remove"/>。</summary>
    public EventHandler Add<T>(Action<T> action)
        where T : Event
        => null!;

    /// <summary>注册一条已装配处理器。</summary>
    public void Add(EventHandler handler)
    {
    }

    /// <summary>移除一条处理器（引用同一性）。</summary>
    public void Remove(EventHandler handler)
    {
    }

    /// <summary>移除满足条件的全部处理器。</summary>
    public void Remove(Predicate<EventHandler> predicate)
    {
    }

    /// <summary>发布事件（同步，按优先级降序直派）。</summary>
    public void Send<T>(T @event)
        where T : Event
    {
    }

    /// <summary>发布事件（异步，线程池整体派发）。</summary>
    public void SendAsync<T>(T @event)
        where T : Event
    {
    }
}
