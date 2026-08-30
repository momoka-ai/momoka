using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace Momoka.Core;

/// <summary>
/// 网关 Hub（SignalR 唯一路由，MapHub "/hubs/gateway"）：操作（request/response）+ 行为上报
/// （request/response，意图 → 主机执行 → 事实广播）。握手校验 query <c>clientId / role / token</c>
/// （token 恒定时间比较；缺省 token / 缺参数 → 断开，fail-fast）。Hub 每次连接新建（transient），
/// 构造注入 <see cref="Gateway"/>。
/// </summary>
public sealed partial class GatewayHub : Hub<IGatewayClient>
{
    private readonly Gateway _gateway;
    private readonly ILogger<GatewayHub> _logger;

    /// <summary>创建 Hub。</summary>
    public GatewayHub(Gateway gateway, ILogger<GatewayHub> logger)
    {
        _gateway = gateway ?? throw new ArgumentNullException(nameof(gateway));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>操作调用（客户端 → 服务器，request/response）：取调用者身份后交由 <see cref="Gateway"/> 执行。</summary>
    public Task<GatewayResponse> InvokeOperation(GatewayRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        Client? caller = _gateway.GetClient(Context.ConnectionId);
        if (caller is null)
        {
            return Task.FromResult(new GatewayResponse(false, null, "Connection is not authenticated."));
        }

        return _gateway.InvokeAsync(request.Id, request.Payload, caller, Context.ConnectionAborted);
    }

    /// <summary>行为上报（客户端 → 主机，request/response）：网关执行行为并经事件总线发布事实。</summary>
    public async Task<GatewayResponse> Post(GatewayRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        Client? caller = _gateway.GetClient(Context.ConnectionId);
        if (caller is null)
        {
            return new GatewayResponse(false, null, "Connection is not authenticated.");
        }

        return await _gateway.HandlePostAsync(request, caller, Context.ConnectionAborted).ConfigureAwait(false);
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

        _gateway.OnConnected(new Client(
            Context.ConnectionId, clientId!, role!, DateTimeOffset.UtcNow,
            Clients.Client(Context.ConnectionId)));
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
