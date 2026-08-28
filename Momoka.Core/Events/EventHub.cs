using System.Reflection;
using System.Runtime.ExceptionServices;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Momoka.Core.Plugins;

namespace Momoka.Core.Events;

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
/// 路由扩展：经 <see cref="RegisterEventType"/> 注册 <see cref="PublishAttribute"/> 类型后，
/// 发布按路由矩阵统一分发（Destination 决定监听者与 wire-out）；wire-sender 由构造注入（宿主接线，无可变 setter）。
/// </remarks>
public sealed partial class EventHub
{
    private readonly object _gate = new();
    private readonly Dictionary<Type, List<Subscription>> _subscriptions = new();
    private readonly Dictionary<Subscribers, List<Subscription>> _bySubscriber = new();
    private readonly Dictionary<Type, RouterRegistration> _routers = new();
    private readonly Dictionary<string, Type> _eventIds = new(StringComparer.Ordinal);
    private readonly ILogger<EventHub> _logger;
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
            foreach (Subscription subscription in subscriptions)
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

            foreach (Subscription subscription in subscriptions)
            {
                if (_subscriptions.TryGetValue(subscription.EventType, out var list))
                {
                    list.Remove(subscription);
                    if (list.Count == 0)
                    {
                        _subscriptions.Remove(subscription.EventType);
                    }
                }
            }
        }
    }

    /// <summary>注册路由事件类型（插件加载时扫描 <see cref="PublishAttribute"/> 调用）；
    /// 组合非法 / 重复 eventId → fail-fast <see cref="InvalidOperationException"/>。</summary>
    public void RegisterEventType(Type type)
    {
        ArgumentNullException.ThrowIfNull(type);

        PublishAttribute? attribute = type.GetCustomAttribute<PublishAttribute>();
        if (attribute is null)
        {
            throw new ArgumentException($"Type '{type}' does not carry [Publish].", nameof(type));
        }

        string? id = NormalizeEventId(attribute.Id);
        ValidateRouting(type, id, attribute);

        lock (_gate)
        {
            if (_routers.ContainsKey(type))
            {
                throw new InvalidOperationException($"Event type '{type}' is already registered.");
            }

            if (id is not null)
            {
                if (!_eventIds.TryAdd(id, type))
                {
                    throw new InvalidOperationException(
                        $"Event id '{id}' is already registered by '{_eventIds[id]}'.");
                }
            }

            _routers.Add(type, new RouterRegistration(type, id, attribute.Destination, attribute.FromClients));
        }
    }

    /// <summary>按声明类型顺序发布事件（属性感知分发）；<typeparamref name="TEvent"/> 应为声明路由时的确切类型。</summary>
    public Task InvokeAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(@event);
        return InvokeCoreAsync(typeof(TEvent), @event, parallel: false, cancellationToken);
    }

    /// <summary>按运行期类型顺序发布事件（wire-in 反序列化后分发的入口）。</summary>
    public Task InvokeAsync(object @event, CancellationToken cancellationToken = default)
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

    /// <summary>按线上 eventId 反查路由事件类型与其 FromClients 标记（Gateway wire-in 用）。</summary>
    internal bool TryGetEventRouter(string eventId, out Type type, out bool fromClients)
    {
        lock (_gate)
        {
            if (_eventIds.TryGetValue(eventId, out Type? resolved))
            {
                type = resolved;
                fromClients = _routers[resolved].FromClients;
                return true;
            }
        }

        type = null!;
        fromClients = false;
        return false;
    }

    /// <summary>扫描监听方法并构造订阅（先全量校验后提交，无部分注册状态）；零监听方法 fail-fast。</summary>
    private List<Subscription> ScanSubscriptions(Subscribers sub, string? source)
    {
        var subscriptions = new List<Subscription>();
        foreach (MethodInfo method in sub.GetType()
            .GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
        {
            SubscribeAttribute? attribute = method.GetCustomAttribute<SubscribeAttribute>();
            if (attribute is null)
            {
                continue;
            }

            subscriptions.Add(CreateSubscription(sub, method, attribute, source));
        }

        if (subscriptions.Count == 0)
        {
            throw new InvalidOperationException(
                $"Subscribers type '{sub.GetType()}' carries no [Subscribe] methods.");
        }

        return subscriptions;
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

        if (method.ReturnType != typeof(void) && method.ReturnType != typeof(Task))
        {
            throw new InvalidOperationException(
                $"[Subscribe] method '{method.DeclaringType?.Name}.{method.Name}' must return Task or void.");
        }

        Func<object, Task> handler = method.ReturnType == typeof(Task)
            ? e => InvokeTaskMethod(subscriber, method, e)
            : e =>
            {
                InvokeMethod(subscriber, method, e);
                return Task.CompletedTask;
            };

        return new Subscription(attribute.Target, handler, attribute.Priority, source);
    }

    private async Task InvokeCoreAsync(
        Type eventType,
        object @event,
        bool parallel,
        CancellationToken cancellationToken)
    {
        RouterRegistration? router;
        lock (_gate)
        {
            _routers.TryGetValue(eventType, out router);
        }

        LogPublished(eventType, @event);

        bool toListeners = router is null
            || router.Destination is EventDestination.Listeners or EventDestination.Everyone;
        bool toWire = router?.Destination is EventDestination.Client or EventDestination.Everyone;

        if (toWire)
        {
            if (_wireSender is not null)
            {
                try
                {
                    await _wireSender(router!.Id!, @event).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    LogWireError(ex, router!.Id!, eventType);
                }
            }
            else
            {
                LogNoWireSender(eventType);
            }
        }

        if (toListeners)
        {
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

    private async Task InvokeSafelyAsync<TEvent>(Subscription subscription, TEvent @event)
    {
        try
        {
            await subscription.InvokeAsync(@event).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            LogHandlerError(ex, typeof(TEvent), subscription.Source);
        }
    }

    private static object? InvokeMethod(object subscriber, MethodInfo method, object @event)
    {
        try
        {
            return method.Invoke(subscriber, new[] { @event });
        }
        catch (TargetInvocationException ex)
        {
            ExceptionDispatchInfo.Capture(ex.InnerException ?? ex).Throw();
            throw; // unreachable
        }
    }

    private static async Task InvokeTaskMethod(object subscriber, MethodInfo method, object @event)
    {
        Task task = (Task)InvokeMethod(subscriber, method, @event)!;
        await task.ConfigureAwait(false);
    }

    private static string? NormalizeEventId(string? id)
        => string.IsNullOrWhiteSpace(id) ? null : id.Trim();

    private static void ValidateRouting(Type type, string? id, PublishAttribute attribute)
    {
        bool needsId = attribute.Destination is EventDestination.Client or EventDestination.Everyone
            || attribute.FromClients;
        if (needsId && id is null)
        {
            throw new InvalidOperationException(
                $"[Publish] on '{type}' requires an Id when Destination is " +
                $"{attribute.Destination} or FromClients is true.");
        }

        if (attribute.FromClients && attribute.Destination != EventDestination.Listeners)
        {
            throw new InvalidOperationException(
                $"[Publish] on '{type}': FromClients requires Destination = Listeners " +
                "(wire-in never echoes back to clients).");
        }

        if (!needsId && id is not null)
        {
            throw new InvalidOperationException(
                $"[Publish] on '{type}' must have an empty Id for Destination {attribute.Destination}.");
        }
    }

    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Error,
        Message = "Event handler for '{EventType}' (source: '{Source}') threw an exception.")]
    private partial void LogHandlerError(Exception exception, Type eventType, string? source);

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

    [LoggerMessage(
        EventId = 4,
        Level = LogLevel.Debug,
        Message = "Event '{EventType}' targets clients but no wire sender is configured.")]
    private partial void LogNoWireSender(Type eventType);

    private sealed record RouterRegistration(Type Type, string? Id, EventDestination Destination, bool FromClients);

    private sealed class Subscription
    {
        public Subscription(Type eventType, Func<object, Task> handler, EventPriority priority, string? source)
        {
            EventType = eventType;
            Handler = handler;
            Priority = priority;
            Source = source;
        }

        public Type EventType { get; }

        public EventPriority Priority { get; }

        public string? Source { get; }

        private Func<object, Task> Handler { get; }

        public Task InvokeAsync<TEvent>(TEvent @event)
        {
            try
            {
                return Handler(@event!);
            }
            catch (Exception ex)
            {
                return Task.FromException(ex);
            }
        }
    }
}
