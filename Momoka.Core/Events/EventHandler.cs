using System;
using Momoka.Core.Plugins;

namespace Momoka.Core.Events;

/// <summary>
/// 一条已注册的监听条目（= 监听器上一个 <see cref="EventHandlerAttribute"/> 方法，或
/// <see cref="EventService.Add{T}(System.Action{T}, Plugin?)"/> 就地封装的临时处理器）。
/// <see cref="Owner"/> 为来源监听器（装配期幂等去重的归属键；null = 临时处理器）；
/// <see cref="Plugin"/> 为来源插件（null = 宿主/无主临时处理器）；
/// <see cref="EventType"/> 与 <see cref="Priority"/> 是公共元数据（分桶/排序用）；
/// <see cref="Action"/> 为已做下转的派发委托（发布直调，无反射）。
/// </summary>
public record class EventHandler(
    IEventHandler? Owner,
    Plugin? Plugin,
    Type EventType,
    Action<Event> Action,
    EventPriority Priority);
