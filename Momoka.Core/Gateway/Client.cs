using System.Text.Json;
using System.Text.Json.Nodes;
using Momoka.Core.Behaviors;

namespace Momoka.Core;

/// <summary>
/// 线上客户端（C/S 的 Client）：手机、电脑、中控屏等物件的一次连接，Gateway 连接注册表的条目，
/// 同时是行为意图来源（<see cref="IntentSource"/>）。<paramref name="direct"/> 为该连接的单向
/// 回拨代理（缺省 = 无 SignalR 宿主，直通消息为 no-op）。
/// </summary>
public sealed class Client : IntentSource
{
    private readonly IGatewayClient? _direct;

    /// <summary>创建客户端。</summary>
    public Client(
        string connectionId,
        string clientId,
        string role,
        DateTimeOffset connectedAt,
        IGatewayClient? direct = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionId);
        ArgumentException.ThrowIfNullOrWhiteSpace(clientId);
        ArgumentException.ThrowIfNullOrWhiteSpace(role);
        ConnectionId = connectionId;
        ClientId = clientId;
        Role = role;
        ConnectedAt = connectedAt;
        _direct = direct;
    }

    /// <summary>SignalR 连接 id（每连接唯一）。</summary>
    public string ConnectionId { get; }

    /// <summary>设备标识（握手 clientId，全局唯一）。</summary>
    public string ClientId { get; }

    /// <summary>角色（握手 role）。</summary>
    public string Role { get; }

    /// <summary>接入时间。</summary>
    public DateTimeOffset ConnectedAt { get; }

    /// <inheritdoc />
    public string Name => ClientId;

    /// <inheritdoc />
    public bool IsRemote => true;

    /// <summary>向本客户端直通发送系统消息（best-effort：无宿主 / 连接已断开静默丢弃）。</summary>
    public async Task SendMessageAsync(string message, CancellationToken cancellationToken = default)
    {
        if (_direct is null)
        {
            return;
        }

        try
        {
            JsonNode node = JsonSerializer.SerializeToNode(new { message }, GatewayJson.Options)!;
            await _direct.ClientEvent("system.message", node).ConfigureAwait(false);
        }
        catch
        {
            // best-effort：连接已断开等情形静默丢弃
        }
    }
}
