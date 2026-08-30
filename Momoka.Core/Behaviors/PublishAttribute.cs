namespace Momoka.Core.Behaviors;

/// <summary>
/// 可传输事件契约（标于具体事件类型上）：携带本属性的类型视作可线上传输的事件，
/// 发布时经 wire-sender 广播全部终端并分发进程内监听者（事件即客户端与主机沟通的桥梁，
/// 默认互相分发）；未携带本属性的类型仅进程内分发。eventId = 类型 FullName
/// （含行为嵌套 <c>Event</c> POD，全局唯一）。行为嵌套 <c>Event</c> 必须携带本属性
/// （由 <see cref="Gateway.RegisterBehavior"/> 契约校验 fail-fast）。
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public sealed class PublishAttribute : Attribute
{
}
