namespace Momoka.Core.Events;

/// <summary>
/// 路由事件的目的地（发布端分发矩阵，见 <see cref="EventRouterAttribute"/>）：
/// 记录器恒记录；是否分发给进程内监听者 / 广播到客户端由本枚举决定。
/// </summary>
public enum EventDestination
{
    /// <summary>仅记录器（sink-only），不进入监听者，也不发客户端。</summary>
    None = 0,

    /// <summary>仅进程内监听者（默认）。</summary>
    Listeners = 1,

    /// <summary>仅发客户端（wire-out），不进进程内监听者；需 <see cref="EventRouterAttribute.Id"/>。</summary>
    Client = 2,

    /// <summary>监听者 + 客户端（wire-out）；需 <see cref="EventRouterAttribute.Id"/>。</summary>
    Everyone = 3,
}
