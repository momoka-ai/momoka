namespace Momoka.Core.Events;

/// <summary>
/// 事件基类（泛型，CRTP）：事件类型 = T 自身，身份即类型（无 Name 字符串）。
/// 每事件类型一张静态处理器表（泛型静态 = 每 TEvent 一份）。
/// 表为复制写（<see cref="volatile"/> 数组整体替换）：发布热路径无锁直接读引用；
/// 注册/退订整体换新数组（写侧竞争极小，插件启用由加载器依序串行）。
/// 退订按来源监听者走 <see cref="Remove"/>（由 <see cref="EventHub.Unregister"/> 反向枚举
/// 处理器接口定位事件类型后调用）。
/// 实现例：<c>public sealed record class EntityPlacedEvent(...) : Event&lt;EntityPlacedEvent&gt;;</c>
/// </summary>
public abstract record class Event<T>
    where T : Event<T>
{
    /// <summary>注册条目表（volatile：写侧整体替换，读侧无锁；数组内容不可变）。</summary>
    public static volatile RegisteredHandler<T>[] Handlers = Array.Empty<RegisteredHandler<T>>();

    /// <summary>登记一个注册条目（复制写 + 优先级降序稳定排序；同级按注册序）。</summary>
    public static void Add(RegisteredHandler<T> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        Handlers = Handlers.Append(handler).OrderByDescending(h => (int)h.Priority).ToArray();
    }

    /// <summary>
    /// 登记一个处理器（<paramref name="source"/> = 监听者实例，<paramref name="handler"/> 即其接口实现），
    /// 复制写入表。由 <see cref="EventHub.Register"/> 反射路由调用。
    /// </summary>
    public static void Register(
        IEventHandler<T> handler,
        object source,
        EventPriority priority = EventPriority.Normal,
        bool ignoreCancelled = false)
    {
        ArgumentNullException.ThrowIfNull(handler);
        Add(new RegisteredHandler<T>(source, handler, priority, ignoreCancelled));
    }

    /// <summary>按来源监听者移除本类型的全部条目（复制写）。</summary>
    public static void Remove(object source)
    {
        ArgumentNullException.ThrowIfNull(source);
        RegisteredHandler<T>[] current = Handlers;
        RegisteredHandler<T>[] filtered = current.Where(h => h.Source != source).ToArray();
        if (filtered.Length != current.Length)
        {
            Handlers = filtered;
        }
    }
}
