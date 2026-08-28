namespace Momoka.Core.Events;

/// <summary>
/// 事件发布属性（标于事件类型上）：声明线上 eventId 与发布目的地。
/// <c>Id</c> 即线上地址：<see cref="EventDestination.Listeners"/> 且带 <c>Id</c> 的事件
/// 接受客户端上报（wire-in 只进监听者，绝不回广播，避免 echo）；<c>Client/Everyone</c> 必须带 <c>Id</c>；
/// <see cref="EventDestination.None"/> 的 <c>Id</c> 必须为空。组合在 <see cref="EventHub.RegisterEventType"/>
/// 注册时 fail-fast 校验。
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public sealed class PublishAttribute : Attribute
{
    /// <summary>线上 eventId（snake_case，全局唯一）；按目的地在注册时校验是否必需/须空。</summary>
    public string? Id { get; set; }

    /// <summary>发布目的地（默认仅进程内监听者）。</summary>
    public EventDestination Destination { get; set; } = EventDestination.Listeners;
}
