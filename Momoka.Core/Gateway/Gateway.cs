using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Momoka.Core;

/// <summary>
/// Ui 网关设施（Core 单例，最小核心）：握手 token + 设备注册表（按 clientId 寻址）+
/// 线上广播原语（EventHub wire-sender 钩子）。客户端 → 主机的请求分发（Post/Query）暂缺，
/// 待真实需求出现时以类型化 handler 形式按需添加。广播经 <see cref="IHubContext{T,T}"/>（构造注入）。
/// </summary>
public sealed partial class Gateway
{
    private readonly object _gate = new();
    private readonly Dictionary<string, Client> _devices = new(StringComparer.Ordinal); // clientId → 设备（主表）
    private readonly Dictionary<string, string> _connections = new(StringComparer.Ordinal); // connectionId → clientId
    private readonly IHubContext<GatewayHub, IGatewayClient>? _hubClients;
    private readonly ILogger<Gateway> _logger;
    private readonly string? _token;

    /// <summary>
    /// 创建网关。<paramref name="hubClients"/> 缺省（无 SignalR 宿主 / 单元测试）时广播为 no-op；
    /// <paramref name="logger"/> 缺省取 NullLogger；<paramref name="token"/> 为握手 token
    /// （缺省空 = 拒绝全部连接）。
    /// </summary>
    public Gateway(
        IHubContext<GatewayHub, IGatewayClient>? hubClients = null,
        ILogger<Gateway>? logger = null,
        string? token = null)
    {
        _hubClients = hubClients;
        _logger = logger ?? NullLogger<Gateway>.Instance;
        _token = token;
    }

    /// <summary>握手 token（Hub 握手校验用）。</summary>
    internal string? Token => _token;

    /// <summary>已连接设备快照（clientId 无序遍历）。</summary>
    public IReadOnlyCollection<Client> Clients
    {
        get
        {
            lock (_gate)
            {
                return _devices.Values.ToList();
            }
        }
    }

    /// <summary>记录连接（GatewayHub.OnConnectedAsync 调用）：按 clientId 注册设备，连接仅作寻址索引。</summary>
    public void OnConnected(Client client)
    {
        ArgumentNullException.ThrowIfNull(client);
        lock (_gate)
        {
            _connections[client.ConnectionId] = client.ClientId;
            _devices[client.ClientId] = client; // 重连覆盖旧条目
        }
    }

    /// <summary>移除连接（GatewayHub.OnDisconnectedAsync 调用）：仅当断开的连接是该设备当前路径时移除设备，防重连竞态。</summary>
    public void OnDisconnected(string connectionId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionId);
        lock (_gate)
        {
            if (_connections.Remove(connectionId, out string? clientId)
                && _devices.TryGetValue(clientId, out Client? client)
                && client.ConnectionId == connectionId)
            {
                _devices.Remove(clientId);
            }
        }
    }

    /// <summary>按连接反查设备（网络层 → 设备 的边界适配，GatewayHub 取调用者用）。</summary>
    internal Client? GetClient(string connectionId)
    {
        lock (_gate)
        {
            return _connections.TryGetValue(connectionId, out string? clientId)
                && _devices.TryGetValue(clientId, out Client? client)
                ? client
                : null;
        }
    }

    /// <summary>广播线上事件（EventHub wire-sender 钩子）：全员发送（v1），序列化失败只记日志。</summary>
    internal async Task BroadcastClientEvent(string eventId, object? payload, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(eventId);

        if (_hubClients is null)
        {
            LogNoHubClients(eventId);
            return;
        }

        try
        {
            JsonNode? node = payload is null
                ? null
                : JsonSerializer.SerializeToNode(payload, GatewayJson.Options);
            await _hubClients.Clients.All.ClientEvent(eventId, node).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            LogBroadcastError(ex, eventId);
        }
    }

    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Debug,
        Message = "Cannot broadcast event '{EventId}': no hub context is configured.")]
    private partial void LogNoHubClients(string eventId);

    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Debug,
        Message = "Broadcast of event '{EventId}' failed.")]
    private partial void LogBroadcastError(Exception exception, string eventId);
}
