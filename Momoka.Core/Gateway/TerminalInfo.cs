namespace Momoka.Core;

/// <summary>已连接终端（连接身份快照）。</summary>
public sealed record TerminalInfo(
    string ConnectionId,
    string TerminalId,
    string Role,
    DateTimeOffset ConnectedAt);
