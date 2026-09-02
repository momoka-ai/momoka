namespace Momoka.Core.Events;

/// <summary>
/// 事件处理器契约（单方法接口）：实现者即监听者，每个实现的 <see cref="IEventHandler{TEvent}"/>
/// 接口对应一类事件的一个处理器。签名由编译器保证（恰一 TEvent 参数、返回 <see cref="Task"/>），
/// 无需方法级反射标记；经 <see cref="EventHub.Register"/> 枚举实例实现的处理器接口统一注册。
/// 监听者可多实现（每事件类型一个接口）；同一实例经多个事件类型的接口即监听多个事件。
/// 优先级 / ignoreCancelled 经类级 <see cref="SubscribeAttribute"/> 提供（缺省 Normal / false）。
/// 实现例：<c>public sealed class MyListener : IEventHandler&lt;MyEvent&gt; { ... }</c>
/// </summary>
public interface IEventHandler<in TEvent>
    where TEvent : Event<TEvent>
{
    /// <summary>处理事件（返回 <see cref="Task"/>，实现即处理器主体）。</summary>
    Task OnInvoke(TEvent e);
}
