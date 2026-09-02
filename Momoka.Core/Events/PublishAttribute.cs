namespace Momoka.Core.Events;

/// <summary>
/// 可传输事件契约（标于具体事件类型上，暂保留）：原语义为发布时经 wire-sender 广播全部终端。
/// 当前事件总线已收口为进程内（发布不触达任何线上终端）；本属性保留待 Packet 期重新定义
/// （eventId = 类型 FullName）。
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public sealed class PublishAttribute : Attribute
{
}
