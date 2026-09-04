namespace Momoka.Core.Events;
using System;

/// <summary>
/// 监听方法标记：附着于事件监听器（实现 <see cref="IEventHandler"/>）的方法上。
/// 事件类型 = 方法参数类型（须为 <see cref="Event"/> 的派生类型，建议直接用具体事件类）；
/// 优先级缺省 <see cref="EventPriority.Normal"/>。
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class EventHandlerAttribute : Attribute
{
    public EventPriority Priority { get; set; } = EventPriority.Normal;
}
