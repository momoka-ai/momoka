namespace Momoka.Core.Events;

/// <summary>
/// 订阅选项（Bukkit @EventHandler 对应物）：**类级**标记，作用于监听者类实现的全部
/// <see cref="EventHandler{TEvent}"/> 接口。缺省 <see cref="Priority"/> = Normal、
/// <see cref="IgnoreCancelled"/> = false。事件类型无需显式——由实现的处理器接口静态决定。
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public sealed class SubscribeAttribute : Attribute
{
    /// <summary>分发优先级（同级按注册序，高者先）。</summary>
    public EventPriority Priority { get; set; } = EventPriority.Normal;

    /// <summary>事件已取消（<see cref="ICancellable.IsCancelled"/>）时是否跳过本处理器。</summary>
    public bool IgnoreCancelled { get; set; }
}
