namespace Momoka.Core.Events;

/// <summary>
/// 事件路由属性（标于事件类型上）：声明线上 eventId、发布目的地与是否接受客户端上报（wire-in）。
/// 路由组合在 <see cref="EventHub.RegisterEventType"/> 注册时 fail-fast 校验：
/// <list type="bullet">
/// <item><c>Destination = Client/Everyone</c> 或 <c>FromClients = true</c> 时必须带 <c>Id</c>；</item>
/// <item><c>FromClients = true</c> 只允许 <c>Destination = Listeners</c>（wire-in 只进监听者，避免 echo）；</item>
/// <item>其余情形 <c>Id</c> 应为空。</item>
/// </list>
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public sealed class EventRouterAttribute : Attribute
{
    /// <summary>线上 eventId（snake_case，全局唯一）；按目的地在注册时校验是否必需/须空。</summary>
    public string? Id { get; set; }

    /// <summary>发布目的地（默认仅进程内监听者）。</summary>
    public EventDestination Destination { get; set; } = EventDestination.Listeners;

    /// <summary>是否接受客户端上报（wire-in）。<c>true</c> 时必须带 <c>Id</c> 且 <see cref="Destination"/> 为 Listeners。</summary>
    public bool FromClients { get; set; }
}
