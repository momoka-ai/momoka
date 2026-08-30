using System.Reflection;
using System.Runtime.ExceptionServices;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Momoka.Core.Plugins;

namespace Momoka.Core.Behaviors;

// 私有簿记的文件级元组别名（无嵌套类型）：
// Subscription = 一条订阅（事件类型 + 优先级 + 来源插件 + 类型擦除后的委托）。
using Subscription = (Type EventType, EventPriority Priority, string? Source, Func<object, Task> Handler);

/// <summary>
/// 事件中心（内存、线程安全）：订阅/退订/发布并发安全；发布时快照订阅表再分发，
/// 分发过程中退订不影响本次快照。订阅只认 <see cref="Subscribers"/> 实现（携带
/// <see cref="SubscribeAttribute"/> 方法的类型）：<see cref="AddSubscribers"/> 扫描注册并按
/// <see cref="EventPriority"/> 降序分发（高者先、同级按注册序），<see cref="RemoveSubscribers"/>
/// 按实例整体退订（幂等）。<see cref="InvokeAsync{TEvent}"/> 默认顺序分发；
/// <see cref="InvokeParallelAsync{TEvent}"/> 并行发布（全部监听者 Task.WhenAll）。
/// handler 异常一律隔离记录，绝不向发布方传播；每次发布写审计日志（Debug）。
/// </summary>
/// <remarks>
/// 事件即客户端与主机沟通的桥梁：携带 <see cref="PublishAttribute"/> 的类型（含行为嵌套
/// <c>Event</c> POD）发布时经 wire-sender 广播全部终端（eventId = 类型 FullName），同时
/// 分发进程内监听者；未携带 <see cref="PublishAttribute"/> 的类型仅进程内分发（可传输契约
/// 在发布路径按属性判定，无需注册表）。wire-sender 由构造注入（宿主接线，无可变 setter）。
/// </remarks>
public sealed partial class EventHub
{
    private readonly Dictionary<Subscribers, List<Subscription>> _bySubscriber = new();
    private readonly object _gate = new();
    private readonly ILogger<EventHub> _logger;
    private readonly Dictionary<Type, List<Subscription>> _subscriptions = new();
    private readonly Func<string, object?, Task>? _wireSender;

    /// <summary>创建事件中心：<paramref name="logger"/> 缺省取 NullLogger（测试/无日志场景）；
    /// <paramref name="wireSender"/> 为线上广播钩子（eventId + 原始载荷，失败只记日志不阻断进程内分发）。</summary>
    public EventHub(ILogger<EventHub>? logger = null, Func<string, object?, Task>? wireSender = null)
    {
        _logger = logger ?? NullLogger<EventHub>.Instance;
        _wireSender = wireSender;
    }

    /// <summary>
    /// 扫描 <paramref name="sub"/> 的 <see cref="SubscribeAttribute"/> 方法并整体注册（实例注册，Bukkit 风格）：
    /// 签名校验（恰一参数 = Target，返回 Task 或 void）与零监听方法 → fail-fast
    /// <see cref="InvalidOperationException"/>；重复注册同一实例 → fail-fast。
    /// </summary>
    public void AddSubscribers(Subscribers sub, Plugin? plugin = null)
    {
        ArgumentNullException.ThrowIfNull(sub);

        List<Subscription> subscriptions = ScanSubscriptions(sub, plugin?.Name);
        lock (_gate)
        {
            if (_bySubscriber.ContainsKey(sub))
            {
                throw new InvalidOperationException(
                    $"Subscribers instance of type '{sub.GetType()}' is already registered.");
            }

            _bySubscriber.Add(sub, subscriptions);
            foreach (var subscription in subscriptions)
            {
                if (!_subscriptions.TryGetValue(subscription.EventType, out var list))
                {
                    list = new List<Subscription>();
                    _subscriptions.Add(subscription.EventType, list);
                }

                list.Add(subscription);
            }
        }
    }

    /// <summary>按声明类型顺序发布事件（携带 <see cref="PublishAttribute"/> 的类型同时广播全部终端）。</summary>
    public Task InvokeAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(@event);
        return InvokeCoreAsync(typeof(TEvent), @event, parallel: false, cancellationToken);
    }

    /// <summary>按运行期类型顺序发布事件（internal：Gateway 行为事实发布 / 反序列化后分发的入口）。</summary>
    internal Task InvokeAsync(object @event, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(@event);
        return InvokeCoreAsync(@event.GetType(), @event, parallel: false, cancellationToken);
    }

    /// <summary>按声明类型**并行**发布事件：全部监听者并发执行，全部完成后返回（异常照常隔离记录）。</summary>
    public Task InvokeParallelAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(@event);
        return InvokeCoreAsync(typeof(TEvent), @event, parallel: true, cancellationToken);
    }

    /// <summary>按实例整体退订（幂等：未注册的实例为 no-op）。</summary>
    public void RemoveSubscribers(Subscribers sub)
    {
        ArgumentNullException.ThrowIfNull(sub);

        lock (_gate)
        {
            if (!_bySubscriber.Remove(sub, out List<Subscription>? subscriptions))
            {
                return;
            }

            foreach (var subscription in subscriptions)
            {
                if (_subscriptions.TryGetValue(subscription.EventType, out var list))
                {
                    // 元组按值相等：四字段（含同一委托实例）全等的才是同一条订阅
                    list.Remove(subscription);
                    if (list.Count == 0)
                    {
                        _subscriptions.Remove(subscription.EventType);
                    }
                }
            }
        }
    }

    private static Subscription CreateSubscription(
        Subscribers subscriber,
        MethodInfo method,
        SubscribeAttribute attribute,
        string? source)
    {
        ParameterInfo[] parameters = method.GetParameters();
        if (parameters.Length != 1 || parameters[0].ParameterType != attribute.Target)
        {
            throw new InvalidOperationException(
                $"[Subscribe] method '{method.DeclaringType?.Name}.{method.Name}' must have exactly " +
                $"one parameter of type '{attribute.Target}'.");
        }

        if (method.ReturnType != typeof(Task) && method.ReturnType != typeof(void))
        {
            throw new InvalidOperationException(
                $"[Subscribe] method '{method.DeclaringType?.Name}.{method.Name}' must return Task or void.");
        }

        Func<object, Task> handler = method.ReturnType == typeof(Task)
            ? e => InvokeHandlerAsync(subscriber, method, e)
            : e =>
            {
                InvokeHandlerAsync(subscriber, method, e).GetAwaiter().GetResult();
                return Task.CompletedTask;
            };

        return (attribute.Target, attribute.Priority, source, handler);
    }

    /// <summary>扫描监听方法并构造订阅（先全量校验后提交，无部分注册状态）；零监听方法 fail-fast。</summary>
    private List<Subscription> ScanSubscriptions(Subscribers sub, string? source)
    {
        List<Subscription> subscriptions = sub.GetType()
            .GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
            .Select(method => (Method: method, Attribute: method.GetCustomAttribute<SubscribeAttribute>()))
            .Where(x => x.Attribute is not null)
            .Select(x => CreateSubscription(sub, x.Method, x.Attribute!, source))
            .ToList();

        return subscriptions.Count == 0
            ? throw new InvalidOperationException(
                $"Subscribers type '{sub.GetType()}' carries no [Subscribe] methods.")
            : subscriptions;
    }

    private async Task DispatchAsync<TEvent>(
        IReadOnlyList<Subscription> snapshot,
        TEvent @event,
        bool parallel,
        CancellationToken cancellationToken)
    {
        var ordered = snapshot.OrderByDescending(s => (int)s.Priority).ToList();

        if (parallel)
        {
            var tasks = new Task[ordered.Count];
            for (int i = 0; i < ordered.Count; i++)
            {
                tasks[i] = InvokeSafelyAsync(ordered[i], @event);
            }

            await Task.WhenAll(tasks).ConfigureAwait(false);
            return;
        }

        foreach (var subscription in ordered)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await InvokeSafelyAsync(subscription, @event).ConfigureAwait(false);
        }
    }

    private async Task InvokeCoreAsync(
        Type eventType,
        object @event,
        bool parallel,
        CancellationToken cancellationToken)
    {
        string eventId = eventType.FullName!;
        LogPublished(eventType, @event);

        if (eventType.GetCustomAttribute<PublishAttribute>() is not null && _wireSender is not null)
        {
            try
            {
                await _wireSender(eventId, @event).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                LogWireError(ex, eventId, eventType);
            }
        }
        else if (_wireSender is null)
        {
            LogNoWireSender(eventType);
        }

        List<Subscription> snapshot;
        lock (_gate)
        {
            _subscriptions.TryGetValue(eventType, out var list);
            snapshot = list is null ? new List<Subscription>() : list.ToList();
        }

        if (snapshot.Count > 0)
        {
            await DispatchAsync(snapshot, @event, parallel, cancellationToken).ConfigureAwait(false);
        }
    }

    private static async Task InvokeHandlerAsync(object subscriber, MethodInfo method, object @event)
    {
        object? returnValue;
        try
        {
            returnValue = method.Invoke(subscriber, new[] { @event });
        }
        catch (TargetInvocationException ex)
        {
            ExceptionDispatchInfo.Capture(ex.InnerException ?? ex).Throw();
            return; // unreachable
        }

        if (returnValue is Task task)
        {
            await task.ConfigureAwait(false);
        }
    }

    private async Task InvokeSafelyAsync<TEvent>(Subscription subscription, TEvent @event)
    {
        try
        {
            await subscription.Handler(@event!).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            LogHandlerError(ex, typeof(TEvent), subscription.Source);
        }
    }

    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Error,
        Message = "Event handler for '{EventType}' (source: '{Source}') threw an exception.")]
    private partial void LogHandlerError(Exception exception, Type eventType, string? source);

    [LoggerMessage(
        EventId = 4,
        Level = LogLevel.Debug,
        Message = "Event '{EventType}' targets clients but no wire sender is configured.")]
    private partial void LogNoWireSender(Type eventType);

    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Debug,
        Message = "Event '{EventType}' published: {@Event}")]
    private partial void LogPublished(Type eventType, object @event);

    [LoggerMessage(
        EventId = 3,
        Level = LogLevel.Error,
        Message = "Wire sender for event '{EventId}' ('{EventType}') threw an exception.")]
    private partial void LogWireError(Exception exception, string eventId, Type eventType);
}
