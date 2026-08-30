using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Momoka.Core.Behaviors;

/// <summary>
/// 主机本地意图来源（Bukkit <c>ConsoleSender</c> 对应物）：语音管线、自动化、Agent 等
/// 非线上模态发起行为时的来源；直通消息落入日志（无线上回拨）。
/// </summary>
public sealed partial class HostSource : IntentSource
{
    private readonly ILogger _logger;

    /// <summary>创建本地来源：<paramref name="origin"/> 为模态标识（如 <c>"voice"</c> / <c>"automation"</c> / <c>"agent"</c>）。</summary>
    public HostSource(string origin, ILogger? logger = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(origin);
        Origin = origin;
        _logger = logger ?? NullLogger.Instance;
    }

    /// <summary>本地模态标识。</summary>
    public string Origin { get; }

    /// <inheritdoc />
    public string Name => Origin;

    /// <inheritdoc />
    public bool IsRemote => false;

    /// <inheritdoc />
    public Task SendMessageAsync(string message, CancellationToken cancellationToken = default)
    {
        LogMessage(Origin, message);
        return Task.CompletedTask;
    }

    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Debug,
        Message = "Host source '{Origin}' received message: {Message}")]
    private partial void LogMessage(string origin, string message);
}
