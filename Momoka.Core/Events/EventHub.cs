using System.Reflection;
using System.Runtime.ExceptionServices;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Momoka.Core.Plugins;

namespace Momoka.Core.Events;

/// <summary>
/// 事件中心（内存、线程安全）：订阅/退订/发布并发安全；发布时快照订阅表再分发，
/// 分发过程中退订不影响本次快照。分发模式由**订阅者**声明（发布者只 await）：
/// Sequential 按优先级与订阅顺序依次执行、Parallel 并发执行、Background 即发即忘。
/// handler 异常一律隔离记录，绝不向发布方传播。
/// </summary>
/// <remarks>
/// 路由扩展：经 <see cref="RegisterEventType"/> 注册 <see cref="EventRouterAttribute"/> 类型后，
/// <see cref="InvokeAsync{TEvent}"/> / <see cref="InvokeAsync(object)"/> 按路由矩阵统一分发
/// （记录器恒记录；Destination 决定监听者与 wire-out）；wire-sender / recorder 由构造注入（宿主接线，无可变 setter）。
/// </remarks>
public sealed partial class EventHub
{
    private readonly object _gate = new();
    private readonly Dictionary<Type, List<Subscription>> _subscriptions = new();
    private readonly Dictionary<Type, RouterRegistration> _routers = new();
    private readonly Dictionary<string, Type> _eventIds = new(StringComparer.Ordinal);
    private readonly ILogger<EventHub> _logger;
    private readonly Func<string, object?, Task>? _wireSender;
    private readonly Func<object, Task>? _recorder;

    /// <summary>创建不记日志的事件中心（测试/无日志场景）。</summary>
    public EventHub()
        : this(NullLogger<EventHub>.Instance)
    {
    }

    /// <summary>创建事件中心。</summary>
    public EventHub(ILogger<EventHub> logger)
        : this(logger, null, null)
    {
    }

    /// <summary>
    /// 创建事件中心并接线路由钩子：<paramref name="wireSender"/>（线上广播，eventId + 原始载荷，
    /// 失败只记日志不阻断进程内分发）、<paramref name="recorder"/>（被动审计 sink，记录全部事件）。
    /// </summary>
    public EventHub(
        ILogger<EventHub> logger,
        Func<string, object?, Task>? wireSender = null,
        Func<object, Task>? recorder = null)
    {
        ArgumentNullException.ThrowIfNull(logger);
        _logger = logger;
        _wireSender = wireSender;
        _recorder = recorder;
    }

    /// <summary>订阅事件（可按订阅者声明分发模式）；返回的令牌用于退订（幂等）。</summary>
    public IDisposable Subscribe<TEvent>(
        Func<TEvent, Task> handler,
        DispatchMode mode = DispatchMode.Sequential)
    {
        ArgumentNullException.ThrowIfNull(handler);

        var subscription = new Subscription(this, typeof(TEvent), mode, e => handler((TEvent)e));
        lock (_gate)
        {
            if (!_subscriptions.TryGetValue(typeof(TEvent), out var list))
            {
                list = new List<Subscription>();
                _subscriptions.Add(typeof(TEvent), list);
            }

            list.Add(subscription);
        }

        return subscription;
    }

    /// <summary>
    /// 扫描 subscriber 的 <see cref="EventSubscribeAttribute"/> 方法并订阅（实例注册，Bukkit 风格）：
    /// 校验签名（恰一参数 = Target，返回 Task 或 void）fail-fast；按 <see cref="EventPriority"/> 排序执行
    /// （高者先、同级按注册序、Monitor 恒最后）；返回令牌 = 整体退订（幂等，插件 OnDisable 用）。
    /// </summary>
    public IDisposable AddSubscribers(object subscriber, Plugin? plugin = null)
    {
        ArgumentNullException.ThrowIfNull(subscriber);

        var tokens = new List<IDisposable>();
        foreach (MethodInfo method in subscriber.GetType()
            .GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
        {
            EventSubscribeAttribute? attribute = method.GetCustomAttribute<EventSubscribeAttribute>();
            if (attribute is null)
            {
                continue;
            }

            try
            {
                tokens.Add(SubscribeScannedMethod(subscriber, method, attribute, plugin?.Name));
            }
            catch
            {
                foreach (IDisposable token in tokens)
                {
                    token.Dispose();
                }

                throw;
            }
        }

        return new BatchDisposable(tokens);
    }

    /// <summary>注册路由事件类型（插件加载时扫描 <see cref="EventRouterAttribute"/> 调用）；
    /// 组合非法 / 重复 eventId → fail-fast <see cref="InvalidOperationException"/>。</summary>
    public void RegisterEventType(Type type)
    {
        ArgumentNullException.ThrowIfNull(type);

        EventRouterAttribute? attribute = type.GetCustomAttribute<EventRouterAttribute>();
        if (attribute is null)
        {
            throw new ArgumentException($"Type '{type}' does not carry [EventRouter].", nameof(type));
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

    /// <summary>按声明类型发布事件（属性感知分发）；<typeparamref name="TEvent"/> 应为声明路由时的确切类型。</summary>
    public Task InvokeAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(@event);
        return InvokeCoreAsync(typeof(TEvent), @event, cancellationToken);
    }

    /// <summary>按运行期类型发布事件（wire-in 反序列化后分发，或 <typeparamref name="TEvent"/> 已知的等价入口）。</summary>
    public Task InvokeAsync(object @event, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(@event);
        return InvokeCoreAsync(@event.GetType(), @event, cancellationToken);
    }

    /// <summary><see cref="InvokeAsync{TEvent}"/> 的兼容别名（更名前的发布入口，语义完全一致）。</summary>
    public Task PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default)
        => InvokeAsync(@event, cancellationToken);

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

    private Subscription SubscribeScannedMethod(
        object subscriber,
        MethodInfo method,
        EventSubscribeAttribute attribute,
        string? source)
    {
        ParameterInfo[] parameters = method.GetParameters();
        if (parameters.Length != 1 || parameters[0].ParameterType != attribute.Target)
        {
            throw new InvalidOperationException(
                $"[EventSubscribe] method '{method.DeclaringType?.Name}.{method.Name}' must have exactly " +
                $"one parameter of type '{attribute.Target}'.");
        }

        if (method.ReturnType != typeof(void) && method.ReturnType != typeof(Task))
        {
            throw new InvalidOperationException(
                $"[EventSubscribe] method '{method.DeclaringType?.Name}.{method.Name}' must return Task or void.");
        }

        Func<object, Task> handler = method.ReturnType == typeof(Task)
            ? e => InvokeTaskMethod(subscriber, method, e)
            : e =>
            {
                InvokeMethod(subscriber, method, e);
                return Task.CompletedTask;
            };

        var subscription = new Subscription(
            this, attribute.Target, DispatchMode.Sequential, handler, attribute.Priority, source);
        lock (_gate)
        {
            if (!_subscriptions.TryGetValue(attribute.Target, out var list))
            {
                list = new List<Subscription>();
                _subscriptions.Add(attribute.Target, list);
            }

            list.Add(subscription);
        }

        return subscription;
    }

    private async Task InvokeCoreAsync(Type eventType, object @event, CancellationToken cancellationToken)
    {
        RouterRegistration? router;
        lock (_gate)
        {
            _routers.TryGetValue(eventType, out router);
        }

        if (_recorder is not null)
        {
            try
            {
                await _recorder(@event).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                LogRecorderError(ex, eventType);
            }
        }

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
                await DispatchAsync(snapshot, @event, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private async Task DispatchAsync<TEvent>(
        IReadOnlyList<Subscription> snapshot,
        TEvent @event,
        CancellationToken cancellationToken)
    {
        var ordered = snapshot
            .OrderBy(s => s.Priority, EventPriorityComparer.Instance)
            .ToList();

        var parallelTasks = new List<Task>();
        foreach (var subscription in ordered)
        {
            switch (subscription.Mode)
            {
                case DispatchMode.Sequential:
                    cancellationToken.ThrowIfCancellationRequested();
                    await InvokeSafelyAsync(subscription, @event).ConfigureAwait(false);
                    break;
                case DispatchMode.Parallel:
                    parallelTasks.Add(InvokeSafelyAsync(subscription, @event));
                    break;
                case DispatchMode.Background:
                    _ = InvokeSafelyAsync(subscription, @event);
                    break;
                default:
                    throw new InvalidOperationException(
                        $"Unsupported dispatch mode '{subscription.Mode}'.");
            }
        }

        if (parallelTasks.Count > 0)
        {
            await Task.WhenAll(parallelTasks).ConfigureAwait(false);
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

    private void Remove(Subscription subscription)
    {
        lock (_gate)
        {
            if (!_subscriptions.TryGetValue(subscription.EventType, out var list))
            {
                return;
            }

            list.Remove(subscription);
            if (list.Count == 0)
            {
                _subscriptions.Remove(subscription.EventType);
            }
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

    private static void ValidateRouting(Type type, string? id, EventRouterAttribute attribute)
    {
        bool needsId = attribute.Destination is EventDestination.Client or EventDestination.Everyone
            || attribute.FromClients;
        if (needsId && id is null)
        {
            throw new InvalidOperationException(
                $"[EventRouter] on '{type}' requires an Id when Destination is " +
                $"{attribute.Destination} or FromClients is true.");
        }

        if (attribute.FromClients && attribute.Destination != EventDestination.Listeners)
        {
            throw new InvalidOperationException(
                $"[EventRouter] on '{type}': FromClients requires Destination = Listeners " +
                "(wire-in never echoes back to clients).");
        }

        if (!needsId && id is not null)
        {
            throw new InvalidOperationException(
                $"[EventRouter] on '{type}' must have an empty Id for Destination {attribute.Destination}.");
        }
    }

    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Error,
        Message = "Event handler for '{EventType}' (source: '{Source}') threw an exception.")]
    private partial void LogHandlerError(Exception exception, Type eventType, string? source);

    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Error,
        Message = "Event recorder for '{EventType}' threw an exception.")]
    private partial void LogRecorderError(Exception exception, Type eventType);

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

    private sealed class Subscription : IDisposable
    {
        private readonly EventHub _hub;
        private int _disposed;

        public Subscription(
            EventHub hub,
            Type eventType,
            DispatchMode mode,
            Func<object, Task> handler,
            EventPriority priority = EventPriority.Normal,
            string? source = null)
        {
            _hub = hub;
            EventType = eventType;
            Mode = mode;
            Priority = priority;
            Source = source;
            Handler = handler;
        }

        public Type EventType { get; }

        public DispatchMode Mode { get; }

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

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
            {
                _hub.Remove(this);
            }
        }
    }

    private sealed class BatchDisposable : IDisposable
    {
        private readonly IReadOnlyList<IDisposable> _tokens;
        private int _disposed;

        public BatchDisposable(IReadOnlyList<IDisposable> tokens)
        {
            _tokens = tokens;
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
            {
                foreach (IDisposable token in _tokens)
                {
                    token.Dispose();
                }
            }
        }
    }

    private sealed class EventPriorityComparer : IComparer<EventPriority>
    {
        public static EventPriorityComparer Instance { get; } = new();

        public int Compare(EventPriority x, EventPriority y)
        {
            bool xMonitor = x == EventPriority.Monitor;
            bool yMonitor = y == EventPriority.Monitor;

            if (xMonitor && yMonitor)
            {
                return 0;
            }

            if (xMonitor)
            {
                return 1;
            }

            if (yMonitor)
            {
                return -1;
            }

            return y.CompareTo(x);
        }
    }
}
