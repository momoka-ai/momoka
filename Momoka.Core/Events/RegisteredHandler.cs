namespace Momoka.Core.Events;

/// <summary>
/// 注册条目（Bukkit RegisteredListener 对应物）：一个监听者接口实现 + 优先级 +
/// ignoreCancelled + 来源监听者。触发期 <see cref="InvokeAsync"/> 经接口方法直调，无反射。
/// </summary>
public sealed class RegisteredHandler<TEvent>
    where TEvent : Event<TEvent>
{
    /// <summary>来源监听者（<see cref="Event{T}.Remove"/> 按实例退订）。</summary>
    public object Source { get; }

    /// <summary>监听者接口实例（订阅者即处理器）。</summary>
    public IEventHandler<TEvent> Handler { get; }

    /// <summary>分发优先级（同级按注册序，高者先）。</summary>
    public EventPriority Priority { get; }

    /// <summary>事件已取消（<see cref="ICancellable.IsCancelled"/>）时是否跳过本处理器。</summary>
    public bool IgnoreCancelled { get; }

    /// <summary>包装一个处理器条目。</summary>
    public RegisteredHandler(
        object source,
        IEventHandler<TEvent> handler,
        EventPriority priority = EventPriority.Normal,
        bool ignoreCancelled = false)
    {
        Source = source ?? throw new ArgumentNullException(nameof(source));
        Handler = handler ?? throw new ArgumentNullException(nameof(handler));
        Priority = priority;
        IgnoreCancelled = ignoreCancelled;
    }

    /// <summary>触发处理器（接口方法直调）。</summary>
    public Task InvokeAsync(TEvent e) => Handler.OnInvoke(e);
}
