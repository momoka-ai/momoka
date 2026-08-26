using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Momoka.Core.Events;

namespace Momoka.Core;

/// <summary>
/// Ui 网关设施（Core 单例）：通用操作路由（request/response）+ 线上事件广播原语 + wire-in 协调 + 终端注册表。
/// 操作由插件 OnEnable 注册（<see cref="RegisterOperation{TRequest,TResponse}"/>，返回幂等注销令牌），
/// 未知操作 / handler 异常 / 反序列化失败一律 fail-soft 返回错误响应。广播经
/// <see cref="IHubContext{T,T}"/>（构造注入）；wire-in 经 <see cref="EventHub"/> 反查注册表并校验 FromClients。
/// </summary>
public sealed partial class Gateway
{
    private readonly object _gate = new();
    private readonly EventHub _events;
    private readonly IHubContext<GatewayHub, IGatewayClient>? _hubClients;
    private readonly ILogger<Gateway> _logger;
    private readonly GatewayOptions _options;
    private readonly Dictionary<string, OperationRegistration> _operations = new(StringComparer.Ordinal);
    private readonly Dictionary<string, TerminalInfo> _terminals = new(StringComparer.Ordinal);

    /// <summary>
    /// 创建网关。<paramref name="hubClients"/> 缺省（无 SignalR 宿主 / 单元测试）时广播为 no-op；
    /// <paramref name="logger"/> / <paramref name="options"/> 缺省取 NullLogger / 空配置。
    /// </summary>
    public Gateway(
        EventHub events,
        IHubContext<GatewayHub, IGatewayClient>? hubClients = null,
        ILogger<Gateway>? logger = null,
        GatewayOptions? options = null)
    {
        _events = events ?? throw new ArgumentNullException(nameof(events));
        _hubClients = hubClients;
        _logger = logger ?? NullLogger<Gateway>.Instance;
        _options = options ?? new GatewayOptions();
    }

    /// <summary>网关配置（Hub 握手校验用）。</summary>
    internal GatewayOptions Options => _options;

    /// <summary>
    /// 注册类型化操作（request/response）；返回令牌用于注销（幂等，插件 OnDisable 用）。
    /// 重复注册同一 operationId → fail-fast <see cref="InvalidOperationException"/>。
    /// </summary>
    public IDisposable RegisterOperation<TRequest, TResponse>(
        string operationId,
        Func<OperationContext, TRequest, CancellationToken, Task<TResponse>> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);

        var registration = new OperationRegistration(
            typeof(TRequest),
            async (ctx, request, ct) => await handler(ctx, (TRequest)request!, ct).ConfigureAwait(false));
        return Register(operationId, registration);
    }

    /// <summary>注册无返回值的操作（void）；其余同 <see cref="RegisterOperation{TRequest,TResponse}"/>。</summary>
    public IDisposable RegisterOperation<TRequest>(
        string operationId,
        Func<OperationContext, TRequest, CancellationToken, Task> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);

        var registration = new OperationRegistration(
            typeof(TRequest),
            async (ctx, request, ct) =>
            {
                await handler(ctx, (TRequest)request!, ct).ConfigureAwait(false);
                return null;
            });
        return Register(operationId, registration);
    }

    /// <summary>
    /// 调用操作：载荷按全局 snake_case 反序列化为 <typeparamref name="TRequest"/>（此处为注册时的请求类型），
    /// 结果序列化回 <see cref="OperationResponse.Payload"/>。未知操作 / handler 异常 / 反序列化失败 → 错误响应（fail-soft）；取消 → "Cancelled"。
    /// </summary>
    public async Task<OperationResponse> InvokeAsync(
        string operationId,
        JsonNode? payload,
        TerminalInfo caller,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(operationId);
        ArgumentNullException.ThrowIfNull(caller);

        OperationRegistration? registration;
        lock (_gate)
        {
            _operations.TryGetValue(operationId, out registration);
        }

        if (registration is null)
        {
            return new OperationResponse(false, null, $"Unknown operation '{operationId}'.");
        }

        if (payload is null && registration.RequestType.IsValueType)
        {
            return new OperationResponse(false, null, $"Operation '{operationId}' requires a payload.");
        }

        try
        {
            object? request = payload?.Deserialize(registration.RequestType, GatewayJson.Options);
            object? result = await registration.Invoke(
                new OperationContext(operationId, caller), request, ct).ConfigureAwait(false);
            JsonNode? resultNode = result is null
                ? null
                : JsonSerializer.SerializeToNode(result, GatewayJson.Options);
            return new OperationResponse(true, resultNode, null);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            return new OperationResponse(false, null, "Cancelled");
        }
        catch (Exception ex)
        {
            return new OperationResponse(false, null, ex.Message);
        }
    }

    /// <summary>已连接终端快照（connectionId 无序遍历）。</summary>
    public IReadOnlyCollection<TerminalInfo> Terminals
    {
        get
        {
            lock (_gate)
            {
                return _terminals.Values.ToList();
            }
        }
    }

    /// <summary>记录连接（GatewayHub.OnConnectedAsync 调用）。</summary>
    public void OnConnected(TerminalInfo terminal)
    {
        ArgumentNullException.ThrowIfNull(terminal);
        lock (_gate)
        {
            _terminals[terminal.ConnectionId] = terminal;
        }
    }

    /// <summary>移除连接（GatewayHub.OnDisconnectedAsync 调用）。</summary>
    public void OnDisconnected(string connectionId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionId);
        lock (_gate)
        {
            _terminals.Remove(connectionId);
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

    /// <summary>
    /// 处理客户端上报（wire-in，GatewayHub.SendEvent 调用）：eventId 注册表反查 →
    /// 校验 <c>FromClients</c> → 反序列化 → 进 <see cref="EventHub"/> 只分发监听者
    /// （绝不自动广播回客户端，避免 echo）。未注册 / 非 FromClients / 载荷非法 → 记日志忽略（fail-soft）。
    /// </summary>
    internal async Task HandleClientEventAsync(string eventId, JsonNode? payload, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(eventId);

        if (!_events.TryGetEventRouter(eventId, out Type type, out bool fromClients))
        {
            LogIgnoredEvent(eventId, "not registered");
            return;
        }

        if (!fromClients)
        {
            LogIgnoredEvent(eventId, "FromClients is false");
            return;
        }

        object? @event;
        try
        {
            @event = payload?.Deserialize(type, GatewayJson.Options);
        }
        catch (Exception ex)
        {
            LogDeserializationFailed(ex, eventId);
            return;
        }

        if (@event is null)
        {
            LogIgnoredEvent(eventId, "empty payload");
            return;
        }

        await _events.InvokeAsync(@event, ct).ConfigureAwait(false);
    }

    /// <summary>按 connectionId 反查终端（GatewayHub 取调用者）。</summary>
    internal TerminalInfo? GetTerminal(string connectionId)
    {
        lock (_gate)
        {
            _terminals.TryGetValue(connectionId, out TerminalInfo? terminal);
            return terminal;
        }
    }

    private OperationToken Register(string operationId, OperationRegistration registration)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(operationId);

        lock (_gate)
        {
            if (_operations.ContainsKey(operationId))
            {
                throw new InvalidOperationException($"Operation '{operationId}' is already registered.");
            }

            _operations.Add(operationId, registration);
        }

        return new OperationToken(this, operationId);
    }

    private void Unregister(string operationId)
    {
        lock (_gate)
        {
            _operations.Remove(operationId);
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

    [LoggerMessage(
        EventId = 3,
        Level = LogLevel.Debug,
        Message = "Ignored client event '{EventId}': {Reason}.")]
    private partial void LogIgnoredEvent(string eventId, string reason);

    [LoggerMessage(
        EventId = 4,
        Level = LogLevel.Debug,
        Message = "Ignored client event '{EventId}': deserialization failed.")]
    private partial void LogDeserializationFailed(Exception exception, string eventId);

    private sealed record OperationRegistration(
        Type RequestType,
        Func<OperationContext, object?, CancellationToken, Task<object?>> Invoke);

    private sealed class OperationToken : IDisposable
    {
        private readonly Gateway _gateway;
        private readonly string _operationId;
        private int _disposed;

        public OperationToken(Gateway gateway, string operationId)
        {
            _gateway = gateway;
            _operationId = operationId;
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
            {
                _gateway.Unregister(_operationId);
            }
        }
    }
}
