namespace Momoka.Core.Events;

/// <summary>
/// 事件订阅属性（标于监听方法上）：声明目标事件类型与执行优先级。
/// 载体类型必须实现 <see cref="Subscribers"/>，由 <see cref="EventHub.AddSubscribers"/> 扫描实例并统一订阅；
/// 签名要求：恰一个参数且其类型等于 <see cref="Target"/>，返回 <see cref="Task"/> 或 <c>void</c>（否则 fail-fast）。
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class SubscribeAttribute : Attribute
{
    /// <summary>创建监听声明。</summary>
    public SubscribeAttribute(Type target)
    {
        Target = target ?? throw new ArgumentNullException(nameof(target));
    }

    /// <summary>目标事件类型。</summary>
    public Type Target { get; }

    /// <summary>执行优先级（默认 <see cref="EventPriority.Normal"/>）。</summary>
    public EventPriority Priority { get; set; } = EventPriority.Normal;
}
