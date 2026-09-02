using System.Collections.Concurrent;
using System.Reflection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Momoka.Core.Events;

/// <summary>
/// 事件中心（Bukkit 风格，CRTP）：进程内订阅/发布（不序列化、绝不跨线，跨线传输归 Packet 层）。
/// 处理器表位于各 <see cref="Event{T}"/> 类型上（泛型静态 + volatile 复制写），
/// 发布热路径无锁直接读；注册期一次反射（枚举实现的 <see cref="IEventHandler{TEvent}"/> 接口 →
/// <see cref="Event{T}.Register"/>），触发期接口直调，无运行期反射、无装箱。
/// handler 异常一律隔离记录，绝不向发布方传播；每次发布写审计日志（Debug）。
/// </summary>
/// <remarks>
/// 阻断（Before）语义：事件实现 <see cref="ICancellable"/> 时，监听者置 <c>IsCancelled = true</c>
/// 即表达否决；标记 <see cref="RegisteredHandler{TEvent}.IgnoreCancelled"/> 的处理器对已取消事件跳过，
/// 其余照常接收（全部否决意见都能被听到），发布方在返回后检查标志决定提交/回滚。
/// 事件类型由插件自声明（派生自 <see cref="Event{T}"/>），Core 不定义业务事件；
/// <see cref="PublishAttribute"/> 保留为未来可传输标记。
/// </remarks>
public sealed class EventHub
{
    private readonly ILogger<EventHub> _logger;
    private readonly ConcurrentDictionary<object, byte> _registered = new();

    private static readonly Action<ILogger, Type, object?, Exception?> LogPublished = LoggerMessage.Define<Type, object?>(
        LogLevel.Debug, new EventId(2), "Event '{EventType}' published: {@Event}");

    private static readonly Action<ILogger, Type, string?, Exception?> LogHandlerError = LoggerMessage.Define<Type, string?>(
        LogLevel.Error, new EventId(1), "Event handler for '{EventType}' (source: '{Source}') threw an exception.");

    /// <summary>创建事件中心：<paramref name="logger"/> 缺省取 NullLogger（测试/无日志场景）。</summary>
    public EventHub(ILogger<EventHub>? logger = null)
    {
        _logger = logger ?? NullLogger<EventHub>.Instance;
    }

    /// <summary>
    /// 注册监听者（Bukkit 风格）：枚举 <paramref name="listener"/> 实现的 <see cref="IEventHandler{TEvent}"/>
    /// 接口，逐个路由到 <c>Event&lt;TEvent&gt;.Register</c>（优先级/ignoreCancelled 取类级
    /// <see cref="SubscribeAttribute"/>，缺省 Normal / false）。零处理器接口 / 重复实例 → fail-fast。
    /// 签名与事件类型由接口静态保证，无方法级校验。
    /// </summary>
    public void Register(object listener)
    {
        ArgumentNullException.ThrowIfNull(listener);
        if (!_registered.TryAdd(listener, 0))
        {
            throw new InvalidOperationException(
                $"Listener of type '{listener.GetType()}' is already registered.");
        }

        SubscribeAttribute? options = listener.GetType().GetCustomAttribute<SubscribeAttribute>();
        int registered = 0;
        foreach (Type handlerInterface in listener.GetType().GetInterfaces())
        {
            if (!handlerInterface.IsGenericType
                || handlerInterface.GetGenericTypeDefinition() != typeof(IEventHandler<>))
            {
                continue;
            }

            Type eventType = handlerInterface.GetGenericArguments()[0];
            MethodInfo register = typeof(Event<>).MakeGenericType(eventType)
                .GetMethod("Register", BindingFlags.Public | BindingFlags.Static)!;
            register.Invoke(null, new object[]
            {
                listener,
                listener,
                options?.Priority ?? EventPriority.Normal,
                options?.IgnoreCancelled ?? false,
            });
            registered++;
        }

        if (registered == 0)
        {
            throw new InvalidOperationException(
                $"Listener type '{listener.GetType()}' implements no IEventHandler<TEvent> interface.");
        }
    }

    /// <summary>
    /// 按实例整体退订（幂等：未注册的实例为 no-op）。与 <see cref="Register"/> 同路径反向：
    /// 枚举 <see cref="IEventHandler{TEvent}"/> 接口 → 由事件类型路由到 <c>Event&lt;TEvent&gt;.Remove</c>。
    /// </summary>
    public void Unregister(object listener)
    {
        ArgumentNullException.ThrowIfNull(listener);
        _registered.TryRemove(listener, out _);

        foreach (Type handlerInterface in listener.GetType().GetInterfaces())
        {
            if (!handlerInterface.IsGenericType
                || handlerInterface.GetGenericTypeDefinition() != typeof(IEventHandler<>))
            {
                continue;
            }

            Type eventType = handlerInterface.GetGenericArguments()[0];
            MethodInfo remove = typeof(Event<>).MakeGenericType(eventType)
                .GetMethod("Remove", BindingFlags.Public | BindingFlags.Static)!;
            remove.Invoke(null, new object[] { listener });
        }
    }

    /// <summary>按声明类型同步顺序发布（进程内）：读 volatile 处理器表 → 按优先级降序直接调用；
    /// 事件实现 <see cref="ICancellable"/> 时，标记 <see cref="RegisteredHandler{TEvent}.IgnoreCancelled"/>
    /// 的处理器对已取消事件跳过（其余照常接收），发布方返回后检查 <c>IsCancelled</c> 决定提交/回滚。</summary>
    public async Task Publish<TEvent>(TEvent e, CancellationToken cancellationToken = default)
        where TEvent : Event<TEvent>
    {
        ArgumentNullException.ThrowIfNull(e);
        LogPublished(_logger, typeof(TEvent), e, null);

        foreach (RegisteredHandler<TEvent> handler in Event<TEvent>.Handlers)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (handler.IgnoreCancelled && e is ICancellable { IsCancelled: true })
            {
                continue;
            }

            try
            {
                await handler.InvokeAsync(e);
            }
            catch (Exception ex)
            {
                LogHandlerError(_logger, typeof(TEvent), handler.Source.GetType().Name, ex);
            }
        }
    }
}
