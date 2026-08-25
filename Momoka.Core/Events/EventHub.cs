using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Momoka.Core.Events;

/// <summary>
/// 事件中心（内存、线程安全）：订阅/退订/发布并发安全；发布时快照订阅表再分发，
/// 分发过程中退订不影响本次快照。分发模式由**订阅者**声明（发布者只 await）：
/// Sequential 按订阅顺序依次执行、Parallel 并发执行、Background 即发即忘。
/// handler 异常一律隔离记录，绝不向发布方传播。
/// </summary>
public sealed partial class EventHub
{
    private readonly object _gate = new();
    private readonly Dictionary<Type, List<Subscription>> _subscriptions = new();
    private readonly ILogger<EventHub> _logger;

    /// <summary>创建不记日志的事件中心（测试/无日志场景）。</summary>
    public EventHub()
        : this(NullLogger<EventHub>.Instance)
    {
    }

    /// <summary>创建事件中心。</summary>
    public EventHub(ILogger<EventHub> logger)
    {
        ArgumentNullException.ThrowIfNull(logger);
        _logger = logger;
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

    /// <summary>发布事件：Sequential/Parallel 订阅者执行完毕后返回；Background 订阅者即发即忘。</summary>
    public Task PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default)
    {
        if (@event is null)
        {
            throw new ArgumentNullException(nameof(@event));
        }

        List<Subscription> snapshot;
        lock (_gate)
        {
            _subscriptions.TryGetValue(typeof(TEvent), out var list);
            snapshot = list is null ? new List<Subscription>() : list.ToList();
        }

        if (snapshot.Count == 0)
        {
            return Task.CompletedTask;
        }

        return DispatchAsync(snapshot, @event, cancellationToken);
    }

    private async Task DispatchAsync<TEvent>(
        IReadOnlyList<Subscription> snapshot,
        TEvent @event,
        CancellationToken cancellationToken)
    {
        var parallelTasks = new List<Task>();
        foreach (var subscription in snapshot)
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
            LogHandlerError(ex, typeof(TEvent));
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

    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Error,
        Message = "Event handler for '{EventType}' threw an exception.")]
    private partial void LogHandlerError(Exception exception, Type eventType);

    private sealed class Subscription : IDisposable
    {
        private readonly EventHub _hub;
        private int _disposed;

        public Subscription(EventHub hub, Type eventType, DispatchMode mode, Func<object, Task> handler)
        {
            _hub = hub;
            EventType = eventType;
            Mode = mode;
            Handler = handler;
        }

        public Type EventType { get; }

        public DispatchMode Mode { get; }

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
}
