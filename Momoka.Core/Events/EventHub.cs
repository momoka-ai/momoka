using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Momoka.Core.Events;

/// <summary>
/// 事件中心（进程内订阅/发布）：纯注册/分发表，不做装配——处理器由插件侧反射封装为
/// <see cref="EventHandler"/> 记录后整体交入。注册表 = <c>事件类型 → EventHandler 数组</c>
/// （不可变数组复制写：发布无锁读快照；写侧低频，由宿主依序调用）。
/// <see cref="Send"/> 同步按优先级降序派发；<see cref="SendAsync"/> 以线程池执行整体派发。
/// handler 异常原样向发布方传播，于首个失败处停止（EventHub 不吞异常、无日志）。
/// </summary>
public sealed class EventHub
{
    private readonly ConcurrentDictionary<Type, EventHandler[]> _handlers = new();

    /// <summary>批量注册（处理器须已装配完成）：逐个 <see cref="Add"/>；
    /// 重复条目由 <see cref="Add"/> 的引用去重 fail-fast。</summary>
    public void AddRange(IEnumerable<EventHandler> handlers)
    {
        foreach (EventHandler handler in handlers)
        {
            Add(handler);
        }
    }

    /// <summary>注册单条处理器：写入其事件类型的监听数组（按优先级降序稳定插入；
    /// 重复实例 fail-fast）。数组整体换新，读侧快照不受影响。</summary>
    public void Add(EventHandler handler)
    {
        EventHandler[] current = _handlers.GetOrAdd(handler.EventType, static _ => Array.Empty<EventHandler>());
        if (current.Any(h => ReferenceEquals(h, handler)))
        {
            throw new InvalidOperationException(
                $"Handler '{handler.GetType().Name}' for event '{handler.EventType.Name}' is already registered.");
        }

        _handlers[handler.EventType] = current.Append(handler).OrderByDescending(h => h.Priority).ToArray();
    }

    /// <summary>批量退订（处理器须已装配完成）：逐条 <see cref="Remove"/>（引用同一性；幂等）。</summary>
    public void RemoveRange(IEnumerable<EventHandler> handlers)
    {
        foreach (EventHandler handler in handlers)
        {
            Remove(handler);
        }
    }

    /// <summary>退订单条处理器（引用同一性；幂等）。</summary>
    public void Remove(EventHandler handler)
    {
        if (!_handlers.TryGetValue(handler.EventType, out EventHandler[]? current))
        {
            return;
        }

        EventHandler[] next = current.Where(h => !ReferenceEquals(h, handler)).ToArray();
        if (next.Length != current.Length)
        {
            _handlers[handler.EventType] = next;
        }
    }

    /// <summary>同步发布：读事件类型（运行时类型）监听数组快照 → 按优先级降序（同级注册序）逐条调用；
    /// 异常向发布方传播并停止。</summary>
    public void Send(Event e)
    {
        if (!_handlers.TryGetValue(e.GetType(), out EventHandler[]? handlers))
        {
            return;
        }

        foreach (EventHandler handler in handlers)
        {
            handler.Action(e);
        }
    }

    /// <summary>异步发布：在线程池执行整体派发（顺序与 <see cref="Send"/> 相同）；
    /// await 完成即全部 handler 执行完毕（异常经 await 原样抛出）。</summary>
    public Task SendAsync(Event e)
        => Task.Run(() => Send(e));
}
