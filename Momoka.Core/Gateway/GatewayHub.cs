using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace Momoka.Core;

/// <summary>
/// 网关 Hub（SignalR 唯一路由，MapHub "/hubs/gateway"）：操作（request/response）+ 线上事件上报（fire-and-forget）。
/// 握手校验 query <c>terminalId / role / token</c>（token 恒定时间比较；缺省 token / 缺参数 → 断开，fail-fast）。
/// Hub 每次连接新建（transient），构造注入 <see cref="Gateway"/>。
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
    public Task<OperationResponse> InvokeOperation(OperationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        TerminalInfo? caller = _gateway.GetTerminal(Context.ConnectionId);
        if (caller is null)
        {
            return Task.FromResult(new OperationResponse(false, null, "Connection is not authenticated."));
        }

        return _gateway.InvokeAsync(request.OperationId, request.Payload, caller, Context.ConnectionAborted);
    }

    /// <summary>线上事件上报（客户端 → 服务器，fire-and-forget）→ 网关 wire-in 协调。</summary>
    public async Task SendEvent(ClientEvent @event)
    {
        ArgumentNullException.ThrowIfNull(@event);

        if (_gateway.GetTerminal(Context.ConnectionId) is null)
        {
            return;
        }

        await _gateway.HandleClientEventAsync(@event.EventId, @event.Payload, Context.ConnectionAborted)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public override async Task OnConnectedAsync()
    {
        var query = Context.GetHttpContext()?.Request.Query;
        string? terminalId = query?["terminalId"];
        string? role = query?["role"];
        string? token = query?["token"];

        if (!IsValidHandshake(_gateway.Options.Token, terminalId, role, token))
        {
            LogRejected(Context.ConnectionId);
            Context.Abort();
            return;
        }

        _gateway.OnConnected(new TerminalInfo(Context.ConnectionId, terminalId!, role!, DateTimeOffset.UtcNow));
        await base.OnConnectedAsync().ConfigureAwait(false);
    }

    /// <inheritdoc />
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        _gateway.OnDisconnected(Context.ConnectionId);
        await base.OnDisconnectedAsync(exception).ConfigureAwait(false);
    }

    private static bool IsValidHandshake(string expectedToken, string? terminalId, string? role, string? token)
    {
        if (string.IsNullOrWhiteSpace(expectedToken)
            || string.IsNullOrWhiteSpace(terminalId)
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
        Message = "Rejected connection '{ConnectionId}': invalid handshake (token / terminalId / role).")]
    private partial void LogRejected(string connectionId);
}
