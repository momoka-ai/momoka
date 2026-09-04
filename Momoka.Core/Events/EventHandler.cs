using System;

namespace Momoka.Core.Events;

/// <summary>
/// 一条已注册的监听条目（= 监听器上一个 <see cref="EventHandlerAttribute"/> 方法）。
/// <see cref="Owner"/> 为来源监听器（装配期幂等去重的归属键：同一监听器整体只装配一次）；
/// <see cref="EventType"/> 与 <see cref="Priority"/> 是公共元数据（EventHub 分桶/排序用）；
/// <see cref="Action"/> 为已做下转的派发委托（发布直调，无反射）。
/// </summary>
public record class EventHandler(
    IEventHandler Owner,
    Type EventType,
    Action<Event> Action,
    EventPriority Priority);