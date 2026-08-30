using System.Linq.Expressions;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Momoka.Core.Behaviors;
using Momoka.Core.Plugins;

namespace Momoka.Core;

/// <summary>
/// Ui 网关设施（Core 单例）：通用操作路由（request/response）+ 线上事件广播原语 + 行为上报管线 + 客户端注册表。
/// 操作由插件 OnEnable 注册（<see cref="RegisterOperation{TRequest,TResponse}"/>，返回幂等注销令牌），
/// 未知操作 / handler 异常 / 反序列化失败一律 fail-soft 返回错误响应。行为经
/// <see cref="RegisterBehavior"/> 由插件加载期扫描注册（四件套契约），客户端 <c>Post</c> 意图 →
/// <see cref="HandlePostAsync"/> 执行并发布事实。广播经 <see cref="IHubContext{T,T}"/>（构造注入）。
/// </summary>
public sealed partial class Gateway
{
    private readonly object _gate = new();
    private readonly Dictionary<string, Client> _clients = new(StringComparer.Ordinal);
    private readonly EventHub _events;
    private readonly IHubContext<GatewayHub, IGatewayClient>? _hubClients;
    private readonly ILogger<Gateway> _logger;
    private readonly Dictionary<string, OperationRegistration> _operations = new(StringComparer.Ordinal);
    private readonly Dictionary<string, PostRegistration> _posts = new(StringComparer.Ordinal);
    private readonly string? _token;

    /// <summary>
    /// 创建网关。<paramref name="hubClients"/> 缺省（无 SignalR 宿主 / 单元测试）时广播为 no-op；
    /// <paramref name="logger"/> 缺省取 NullLogger；<paramref name="token"/> 为握手 token
    /// （缺省空 = 拒绝全部连接）。
    /// </summary>
    public Gateway(
        EventHub events,
        IHubContext<GatewayHub, IGatewayClient>? hubClients = null,
        ILogger<Gateway>? logger = null,
        string? token = null)
    {
        _events = events ?? throw new ArgumentNullException(nameof(events));
        _hubClients = hubClients;
        _logger = logger ?? NullLogger<Gateway>.Instance;
        _token = token;
    }

    /// <summary>握手 token（Hub 握手校验用）。</summary>
    internal string? Token => _token;

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
    /// 结果序列化回 <see cref="GatewayResponse.Payload"/>。未知操作 / handler 异常 / 反序列化失败 → 错误响应（fail-soft）；取消 → "Cancelled"。
    /// </summary>
    public async Task<GatewayResponse> InvokeAsync(
        string operationId,
        JsonNode? payload,
        Client caller,
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
            return new GatewayResponse(false, null, $"Unknown operation '{operationId}'.");
        }

        if (payload is null && registration.RequestType.IsValueType)
        {
            return new GatewayResponse(false, null, $"Operation '{operationId}' requires a payload.");
        }

        try
        {
            object? request = payload?.Deserialize(registration.RequestType, GatewayJson.Options);
            object? result = await registration.Invoke(
                new OperationContext(operationId, caller), request, ct).ConfigureAwait(false);
            JsonNode? resultNode = result is null
                ? null
                : JsonSerializer.SerializeToNode(result, GatewayJson.Options);
            return new GatewayResponse(true, resultNode, null);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            return new GatewayResponse(false, null, "Cancelled");
        }
        catch (Exception ex)
        {
            return new GatewayResponse(false, null, ex.Message);
        }
    }

    /// <summary>
    /// 扫描期注册行为（插件加载时调用）：校验四件套契约（<see cref="Behavior{T}"/> 派生 + 具体类型 +
    /// 嵌套 <c>Intent</c> + 嵌套携带 <see cref="PublishAttribute"/> 的 <c>Event</c> + 公开实例
    /// <c>Execute(Intent, IntentSource?)</c> 且返回其 <c>Event</c>），实例化（须公开无参构造器）并注入
    /// 宿主，构建类型擦除执行委托。重复注册同一 eventId → fail-fast <see cref="InvalidOperationException"/>。
    /// </summary>
    internal void RegisterBehavior(Type behaviorType, PluginService? host = null)
    {
        ArgumentNullException.ThrowIfNull(behaviorType);

        Type? behaviorBase = FindBehaviorBase(behaviorType);
        if (behaviorBase is null)
        {
            throw new ArgumentException(
                $"Type '{behaviorType}' does not derive from Behavior<T>.", nameof(behaviorType));
        }

        if (behaviorType.IsAbstract || behaviorType.IsInterface)
        {
            throw new InvalidOperationException(
                $"Behavior '{behaviorType}' must be concrete (instantiable).");
        }

        Type? intentType = behaviorType.GetNestedType("Intent")
            ?? throw new ArgumentException(
                $"Behavior '{behaviorType}' must declare a nested Intent record.", nameof(behaviorType));

        Type? eventType = behaviorType.GetNestedType("Event")
            ?? throw new ArgumentException(
                $"Behavior '{behaviorType}' must declare a nested Event record.", nameof(behaviorType));

        if (eventType.GetCustomAttribute<PublishAttribute>() is null)
        {
            throw new ArgumentException(
                $"Behavior '{behaviorType}' nested Event must carry [Publish].", nameof(behaviorType));
        }

        MethodInfo? execute = behaviorType.GetMethod(
            "Execute",
            BindingFlags.Public | BindingFlags.Instance,
            binder: null,
            new[] { intentType, typeof(IntentSource) },
            modifiers: null);
        if (execute is null)
        {
            throw new ArgumentException(
                $"Behavior '{behaviorType}' must declare a public Execute(Intent, IntentSource?) method.",
                nameof(behaviorType));
        }

        if (execute.ReturnType != eventType)
        {
            throw new ArgumentException(
                $"Behavior '{behaviorType}' Execute must return its nested Event type.", nameof(behaviorType));
        }

        object instance;
        try
        {
            instance = Activator.CreateInstance(behaviorType, nonPublic: false)!;
        }
        catch (Exception ex) when (ex is MemberAccessException or TargetInvocationException
            or TypeLoadException or TypeInitializationException or NotSupportedException)
        {
            throw new ArgumentException(
                $"Behavior '{behaviorType}' could not be instantiated (requires a public parameterless constructor).",
                nameof(behaviorType));
        }

        if (host is not null)
        {
            MethodInfo injectHost = behaviorBase.GetMethod(
                "InjectHost", BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException($"Behavior base '{behaviorBase}' must declare InjectHost.");
            injectHost.Invoke(instance, new object[] { host });
        }

        var intentParam = Expression.Parameter(typeof(object), "intent");
        var sourceParam = Expression.Parameter(typeof(IntentSource), "source");
        var call = Expression.Call(
            Expression.Constant(instance),
            execute,
            Expression.Convert(intentParam, intentType),
            sourceParam);
        Func<object, IntentSource?, object> bridge = Expression.Lambda<Func<object, IntentSource?, object>>(
            Expression.Convert(call, typeof(object)), intentParam, sourceParam).Compile();

        var registration = new PostRegistration(eventType, intentType, bridge);
        lock (_gate)
        {
            if (!_posts.TryAdd(eventType.FullName!, registration))
            {
                throw new InvalidOperationException(
                    $"Event '{eventType.FullName}' is already registered.");
            }
        }
    }

    /// <summary>
    /// 行为上报管线（wire-in 唯一入口，GatewayHub.Post 调用）：注册表反查 → 反序列化意图 →
    /// 实例执行生成规范事实 → 经 <see cref="EventHub"/> 发布（[Publish] 类型广播全部终端 + 监听者）。
    /// 未知事件 / 反序列化失败 / Execute 异常 → 错误回执（fail-soft）；取消 → "Cancelled"。
    /// </summary>
    internal async Task<GatewayResponse> HandlePostAsync(
        GatewayRequest request,
        Client client,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(client);

        PostRegistration? registration;
        lock (_gate)
        {
            _posts.TryGetValue(request.Id, out registration);
        }

        if (registration is null)
        {
            return new GatewayResponse(false, null, $"Unknown event '{request.Id}'.");
        }

        object? intent;
        try
        {
            intent = request.Payload?.Deserialize(registration.IntentType, GatewayJson.Options);
        }
        catch (Exception ex)
        {
            return new GatewayResponse(false, null, $"Deserialization failed: {ex.Message}");
        }

        if (intent is null)
        {
            return new GatewayResponse(false, null, "Post requires a payload.");
        }

        try
        {
            object fact = registration.Execute(intent, client);
            await _events.InvokeAsync(fact, ct).ConfigureAwait(false);
            return new GatewayResponse(true, null, null);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            return new GatewayResponse(false, null, "Cancelled");
        }
        catch (Exception ex)
        {
            return new GatewayResponse(false, null, ex.Message);
        }
    }

    /// <summary>已连接客户端快照（connectionId 无序遍历）。</summary>
    public IReadOnlyCollection<Client> Clients
    {
        get
        {
            lock (_gate)
            {
                return _clients.Values.ToList();
            }
        }
    }

    /// <summary>记录连接（GatewayHub.OnConnectedAsync 调用）。</summary>
    public void OnConnected(Client client)
    {
        ArgumentNullException.ThrowIfNull(client);
        lock (_gate)
        {
            _clients[client.ConnectionId] = client;
        }
    }

    /// <summary>移除连接（GatewayHub.OnDisconnectedAsync 调用）。</summary>
    public void OnDisconnected(string connectionId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionId);
        lock (_gate)
        {
            _clients.Remove(connectionId);
        }
    }

    /// <summary>按 connectionId 反查客户端（GatewayHub 取调用者）。</summary>
    internal Client? GetClient(string connectionId)
    {
        lock (_gate)
        {
            _clients.TryGetValue(connectionId, out Client? client);
            return client;
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

    private static Type? FindBehaviorBase(Type type)
    {
        Type? current = type.BaseType;
        while (current is not null)
        {
            if (current.IsGenericType && current.GetGenericTypeDefinition() == typeof(Behavior<>))
            {
                return current;
            }

            current = current.BaseType;
        }

        return null;
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

    private sealed record PostRegistration(
        Type EventType,
        Type IntentType,
        Func<object, IntentSource?, object> Execute);
}
