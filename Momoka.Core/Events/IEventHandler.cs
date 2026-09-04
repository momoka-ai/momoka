namespace Momoka.Core.Events;

/// <summary>
/// 事件监听器契约（标记接口，无成员）：插件监听器实现本接口，在要监听的方法上标记
/// <see cref="EventHandlerAttribute"/>；一个监听器可含多个监听方法（可跨多个事件类型）。
/// 装配发生在插件侧：带标记方法被反射封装为 <see cref="EventHandler"/> 记录，
/// 宿主启用/停用时随插件整体交给 <see cref="EventHub"/> 注册/退订。
/// </summary>
public interface IEventHandler
{
}
