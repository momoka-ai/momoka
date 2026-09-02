using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace Momoka.Core;

/// <summary>
/// 网关 Hub（SignalR 唯一路由，MapHub "/hubs/gateway"）：握手（query <c>clientId / role / token</c>
/// 校验，token 恒定时间比较；缺省 token / 缺参数 → 断开，fail-fast）+ 连接注册/注销。客户端 → 主机
/// 的请求方法与下行通道随 Packet 层实现（见 DESIGN_CORE §11）。Hub 每次连接新建（transient），
/// 构造注入 <see cref="Gateway"/>。
/// </summary>
public sealed partial class GatewayHub : Hub
{
    private readonly Gateway _gateway;
    private readonly ILogger<GatewayHub> _logger;

    /// <summary>创建 Hub。</summary>
    public GatewayHub(Gateway gateway, ILogger<GatewayHub> logger)
    {
        _gateway = gateway ?? throw new ArgumentNullException(nameof(gateway));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public override async Task OnConnectedAsync()
    {
        var query = Context.GetHttpContext()?.Request.Query;
        string? clientId = query?["clientId"];
        string? role = query?["role"];
        string? token = query?["token"];

        if (!IsValidHandshake(_gateway.Token, clientId, role, token))
        {
            LogRejected(Context.ConnectionId);
            Context.Abort();
            return;
        }

        _gateway.OnConnected(new Client(clientId!, role!, DateTimeOffset.UtcNow, Context.ConnectionId));
        await base.OnConnectedAsync().ConfigureAwait(false);
    }

    /// <inheritdoc />
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        _gateway.OnDisconnected(Context.ConnectionId);
        await base.OnDisconnectedAsync(exception).ConfigureAwait(false);
    }

    private static bool IsValidHandshake(string? expectedToken, string? clientId, string? role, string? token)
    {
        if (string.IsNullOrWhiteSpace(expectedToken)
            || string.IsNullOrWhiteSpace(clientId)
            || string.IsNullOrWhiteSpace(role))
        {
            return false;
        }

        return !string.IsNullOrEmpty(token) && FixedTimeEquals(token, expectedToken);
    }

    private static bool FixedTimeEquals(string provided, string expected)
    {
        byte[] providedBytes = Encoding.UTF8.GetBytes(provided);
        byte[] expectedBytes = Encoding.UTF8.GetBytes(expected);
        return providedBytes.Length == expectedBytes.Length
            && CryptographicOperations.FixedTimeEquals(providedBytes, expectedBytes);
    }

    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Warning,
        Message = "Rejected connection '{ConnectionId}': invalid handshake (token / clientId / role).")]
    private partial void LogRejected(string connectionId);
}
