using Microsoft.Extensions.Logging;

namespace Momoka.Core.Events;

/// <summary>
/// 事件记录器（被动审计 sink）：记录发布进 <see cref="EventHub"/> 的全部事件，本期为 ILogger 后端。
/// 由宿主接线为 EventHub 的 recorder 钩子（<see cref="EventHub"/> 构造注入，零序列化知识）。
/// </summary>
public sealed partial class EventRecorder
{
    private readonly ILogger<EventRecorder> _logger;

    /// <summary>创建事件记录器。</summary>
    public EventRecorder(ILogger<EventRecorder> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>记录一条事件（Debug 级结构化日志，含事件类型与载荷快照）。</summary>
    public Task RecordAsync(object @event, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(@event);
        LogRecorded(@event.GetType(), @event);
        return Task.CompletedTask;
    }

    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Debug,
        Message = "Event '{EventType}' recorded: {@Event}")]
    private partial void LogRecorded(Type eventType, object @event);
}
