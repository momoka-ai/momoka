using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Momoka.Core.Plugins;

namespace Momoka.Core.Events;

/// <summary>
/// 事件服务（进程内订阅/发布的唯一总线）：纯注册/分发表，装配在插件侧完成——插件监听器经
/// <c>Plugin.AddEventHandler</c> 反射封装为 <see cref="EventHandler"/> 记录整体交入；宿主/插件亦可经
/// <see cref="Add{T}(Action{T}, Plugin?)"/> 就地封装临时处理器（返回可移除句柄）。
/// 注册表 = <c>事件类型 → EventHandler 数组</c>（不可变数组复制写：发布无锁读快照；写侧低频，由宿主依序调用）。
/// <see cref="Send"/> 同步按优先级降序派发；<see cref="SendAsync"/> 以线程池执行整体派发。
/// handler 异常原样向发布方传播，于首个失败处停止（不吞异常、无日志）。
/// </summary>
public sealed class EventService
{
    private readonly ConcurrentDictionary<Type, EventHandler[]> _handlers = new();

    /// <summary>批量注册（处理器须已装配完成）：逐个 <see cref="Add(EventHandler)"/>；重复条目 fail-fast。</summary>
    public void AddRange(IEnumerable<EventHandler> handlers)
    {
        foreach (EventHandler handler in handlers)
        {
            Add(handler);
        }
    }

    /// <summary>注册单条处理器：写入其事件类型的监听数组（按优先级降序稳定插入；同实例重复 fail-fast）。</summary>
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

    /// <summary>就地封装临时处理器（<paramref name="action"/> 直接转派，无反射）并注册；
    /// 返回的 <see cref="EventHandler"/> 句柄可交给 <see cref="Remove(EventHandler)"/> 定向退订。</summary>
    public EventHandler Add<T>(Action<T> action, Plugin? plugin = null)
        where T : Event
    {
        EventHandler handler = new(null, plugin, typeof(T), e => action((T)e), EventPriority.Normal);
        Add(handler);
        return handler;
    }

    /// <summary>批量退订（处理器须已装配完成）：逐条 <see cref="Remove(EventHandler)"/>（引用同一性；幂等）。</summary>
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
            if (next.Length == 0)
            {
                _handlers.TryRemove(handler.EventType, out _);
            }
            else
            {
                _handlers[handler.EventType] = next;
            }
        }
    }

    /// <summary>移除满足 <paramref name="predicate"/> 的全部处理器（跨事件桶；空桶随之清理）。</summary>
    public void Remove(Predicate<EventHandler> predicate)
    {
        foreach (Type eventType in _handlers.Keys)
        {
            if (!_handlers.TryGetValue(eventType, out EventHandler[]? current))
            {
                continue;
            }

            EventHandler[] next = current.Where(h => !predicate(h)).ToArray();
            if (next.Length == current.Length)
            {
                continue;
            }

            if (next.Length == 0)
            {
                _handlers.TryRemove(eventType, out _);
            }
            else
            {
                _handlers[eventType] = next;
            }
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

    /// <summary>强类型壳：编译期限定事件类型，运行时仍按 <paramref name="e"/> 的实际类型分桶。</summary>
    public void Send<T>(T e)
        where T : Event
        => Send((Event)e);

    /// <summary>异步发布：在线程池执行整体派发（顺序与 <see cref="Send(Event)"/> 相同）；
    /// await 完成即全部 handler 执行完毕（异常经 await 原样抛出）。</summary>
    public Task SendAsync(Event e)
        => Task.Run(() => Send(e));

    /// <summary>强类型壳：<see cref="SendAsync(Event)"/> 的编译期限定版本。</summary>
    public Task SendAsync<T>(T e)
        where T : Event
        => SendAsync((Event)e);
}
