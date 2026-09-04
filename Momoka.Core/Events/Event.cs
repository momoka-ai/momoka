namespace Momoka.Core.Events;

/// <summary>
/// 事件基类（纯类型载体，无成员）：事件类型 = 实例的运行时具体类型。
/// 监听器以 [<see cref="EventHandlerAttribute"/>] 方法声明所监听的事件类（方法参数类型）；
/// <see cref="EventHub.Send"/> 按运行时类型精确分桶派发（不支持按基类订阅派生事件）。
/// </summary>
public abstract record class Event
{
}
