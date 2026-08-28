namespace Momoka.Core.Events;

/// <summary>
/// 事件监听者标记接口（Bukkit Listener 风格）：所有携带 <see cref="SubscribeAttribute"/>
/// 方法的类型必须实现本接口；<see cref="EventHub.AddSubscribers"/> / <see cref="EventHub.RemoveSubscribers"/>
/// 只接受本接口实例。实现类自行管理依赖（构造注入或插件内构造），EventHub 不实例化监听者。
/// </summary>
public interface Subscribers
{
}
