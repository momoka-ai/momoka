using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Momoka.Core.Events;

/// <summary>
/// 内存事件总线实现（线程安全）：订阅/退订/发布并发安全；发布时快照订阅表再分发，
/// 分发过程中退订不影响本次快照。单 handler 异常隔离，绝不向发布方传播。
/// </summary>
public sealed partial class EventBus : IEventBus
{
    private readonly object _gate = new();
    private readonly Dictionary<Type, List<Subscription>> _subscriptions = new();
    private readonly ILogger<EventBus> _logger;

    /// <summary>创建不记日志的事件总线（测试/无日志场景）。</summary>
    public EventBus()
        : this(NullLogger<EventBus>.Instance)
    {
    }

    /// <summary>创建事件总线。</summary>
    public EventBus(ILogger<EventBus> logger)
    {
        ArgumentNullException.ThrowIfNull(logger);
        _logger = logger;
    }

    /// <inheritdoc />
    public IDisposable Subscribe<TEvent>(Func<TEvent, Task> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);

        var subscription = new Subscription(this, typeof(TEvent), e => handler((TEvent)e));
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

    /// <inheritdoc />
    public Task PublishAsync<TEvent>(
        TEvent @event,
        DispatchMode mode = DispatchMode.Sequential,
        CancellationToken cancellationToken = default)
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

        return mode switch
        {
            DispatchMode.Sequential => InvokeSequentialAsync(snapshot, @event, cancellationToken),
            DispatchMode.Parallel => InvokeParallelAsync(snapshot, @event),
            DispatchMode.Background => InvokeBackgroundAsync(snapshot, @event),
            _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, null),
        };
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

    private async Task InvokeSequentialAsync<TEvent>(
        IReadOnlyList<Subscription> snapshot,
        TEvent @event,
        CancellationToken cancellationToken)
    {
        foreach (var subscription in snapshot)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                await subscription.InvokeAsync(@event).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                LogHandlerError(ex, typeof(TEvent));
            }
        }
    }

    private async Task InvokeParallelAsync<TEvent>(IReadOnlyList<Subscription> snapshot, TEvent @event)
    {
        var tasks = snapshot.Select(s => s.InvokeAsync(@event)).ToArray();
        var aggregate = await Task.WhenAll(tasks)
            .ContinueWith(t => t.Exception, TaskScheduler.Default)
            .ConfigureAwait(false);
        if (aggregate is not null)
        {
            LogAggregateHandlerError(aggregate, typeof(TEvent));
        }
    }

    private async Task InvokeBackgroundAsync<TEvent>(IReadOnlyList<Subscription> snapshot, TEvent @event)
    {
        foreach (var subscription in snapshot)
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
    }

    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Error,
        Message = "Event handler for '{EventType}' threw an exception.")]
    private partial void LogHandlerError(Exception exception, Type eventType);

    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Error,
        Message = "One or more event handlers for '{EventType}' threw exceptions.")]
    private partial void LogAggregateHandlerError(Exception exception, Type eventType);

    private sealed class Subscription : IDisposable
    {
        private readonly EventBus _bus;
        private int _disposed;

        public Subscription(EventBus bus, Type eventType, Func<object, Task> handler)
        {
            _bus = bus;
            EventType = eventType;
            Handler = handler;
        }

        public Type EventType { get; }

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
                _bus.Remove(this);
            }
        }
    }
}
