namespace Momoka.Core;

/// <summary>
/// Ui 网关设施（Core 单例，最小核心）：握手 token + 设备注册表（按 clientId 寻址）+
/// 连接路径索引（connectionId → clientId，重连竞态安全）。客户端 → 主机的请求分发
/// （Post/Packet）与线上广播原语随 Packet 层实现（见 DESIGN_CORE §11）。
/// </summary>
public sealed class Gateway
{
    private readonly object _gate = new();
    private readonly Dictionary<string, Client> _devices = new(StringComparer.Ordinal); // clientId → 设备（主表）
    private readonly Dictionary<string, string> _connections = new(StringComparer.Ordinal); // connectionId → clientId
    private readonly string? _token;

    /// <summary>
    /// 创建网关。<paramref name="token"/> 为握手 token（缺省空 = 拒绝全部连接）。
    /// </summary>
    public Gateway(string? token = null)
    {
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
}
