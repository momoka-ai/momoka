using System.Reflection;
using System.Runtime.Loader;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Momoka.Core;
using Momoka.Core.Events;
using Momoka.Core.Plugins;
using Xunit;

namespace Momoka.Core.Tests;

/// <summary>
/// 网关集成测试（自建内联 WebApplication + TestServer + SignalR 客户端，不走 Program.Main）：
/// 操作往返（snake_case）/ 错误响应 / wire-in 全流程 / 鉴权拒绝 / 终端注册表 / 插件路由注册 / 事件记录器。
/// </summary>
public sealed class GatewayTests
{
    [Fact]
    public async Task InvokeOperation_RoundTrip_UsesSnakeCase()
    {
        await using var harness = await GatewayHarness.CreateAsync();
        harness.Gateway.RegisterOperation<SetLightRequest, SetLightResponse>("set_light", (_, req, _) =>
            Task.FromResult(new SetLightResponse($"ok:{req.RoomName}:{req.Brightness}")));

        await using var connection = await harness.ConnectAsync();
        var response = await connection.InvokeCoreAsync<OperationResponse>("InvokeOperation", new object[]
        {
            new OperationRequest("set_light", JsonNode.Parse("""{"brightness":80,"room_name":"kitchen"}""")),
        });

        Assert.True(response.Success);
        Assert.Null(response.Error);
        Assert.Equal("ok:kitchen:80", response.Payload!["message"]!.GetValue<string>());
    }

    [Fact]
    public async Task InvokeOperation_VoidOperation_ReturnsSuccessWithNullPayload()
    {
        await using var harness = await GatewayHarness.CreateAsync();
        var executed = false;
        harness.Gateway.RegisterOperation<string>("flip", (_, value, _) =>
        {
            executed = value == "on";
            return Task.CompletedTask;
        });

        await using var connection = await harness.ConnectAsync();
        var response = await connection.InvokeCoreAsync<OperationResponse>("InvokeOperation", new object[]
        {
            new OperationRequest("flip", JsonNode.Parse("\"on\"")),
        });

        Assert.True(response.Success);
        Assert.Null(response.Payload);
        Assert.Null(response.Error);
        Assert.True(executed);
    }

    [Fact]
    public async Task InvokeOperation_UnknownOperation_ReturnsError()
    {
        await using var harness = await GatewayHarness.CreateAsync();
        await using var connection = await harness.ConnectAsync();

        var response = await connection.InvokeCoreAsync<OperationResponse>("InvokeOperation", new object[]
        {
            new OperationRequest("no_such_op", null),
        });

        Assert.False(response.Success);
        Assert.Contains("Unknown operation", response.Error);
    }

    [Fact]
    public async Task InvokeOperation_HandlerException_ReturnsError()
    {
        await using var harness = await GatewayHarness.CreateAsync();
        harness.Gateway.RegisterOperation<string>("boom", (_, _, _) =>
            throw new InvalidOperationException("handler exploded"));

        await using var connection = await harness.ConnectAsync();
        var response = await connection.InvokeCoreAsync<OperationResponse>("InvokeOperation", new object[]
        {
            new OperationRequest("boom", null),
        });

        Assert.False(response.Success);
        Assert.Contains("handler exploded", response.Error);
    }

    [Fact]
    public async Task InvokeOperation_DeserializationFailure_ReturnsError()
    {
        await using var harness = await GatewayHarness.CreateAsync();
        harness.Gateway.RegisterOperation<SetLightRequest, SetLightResponse>("set_light", (_, req, _) =>
            Task.FromResult(new SetLightResponse(req.RoomName)));

        await using var connection = await harness.ConnectAsync();
        var response = await connection.InvokeCoreAsync<OperationResponse>("InvokeOperation", new object[]
        {
            new OperationRequest("set_light", JsonNode.Parse("""{"brightness":"high","room_name":"kitchen"}""")),
        });

        Assert.False(response.Success);
        Assert.Contains("brightness", response.Error);
    }

    [Fact]
    public async Task SendEvent_WireIn_HandledByPlugin_AndBroadcastToAllTerminals()
    {
        await using var harness = await GatewayHarness.CreateAsync();
        ResetEventsPluginLog();
        LoadEventsPlugin(harness);

        await using var first = await harness.ConnectAsync(terminalId: "ui-1");
        await using var second = await harness.ConnectAsync(terminalId: "ui-2");
        var received = new TaskCompletionSource<(string EventId, JsonNode? Payload)>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var receivedSecond = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        first.On<string, JsonNode?>("ClientEvent", (eventId, payload) =>
        {
            received.TrySetResult((eventId, payload));
            return Task.CompletedTask;
        });
        second.On<string, JsonNode?>("ClientEvent", (eventId, _) =>
        {
            receivedSecond.TrySetResult(eventId);
            return Task.CompletedTask;
        });

        await first.SendCoreAsync("SendEvent", new object[]
        {
            new ClientEvent("report_event", JsonNode.Parse("""{"message":"hi"}""")),
        });

        var (eventId, payload) = await received.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal("announce_event", eventId);
        Assert.Equal("hi", payload!["message"]!.GetValue<string>());
        Assert.Equal("announce_event", await receivedSecond.Task.WaitAsync(TimeSpan.FromSeconds(10)));
        Assert.Equal(new[] { "report:hi" }, EventsPluginLog());
    }

    [Fact]
    public async Task SendEvent_UnregisteredOrNotReportable_IsIgnored()
    {
        await using var harness = await GatewayHarness.CreateAsync();
        ResetEventsPluginLog();
        LoadEventsPlugin(harness);

        await using var connection = await harness.ConnectAsync();
        var received = false;
        connection.On<string, JsonNode?>("ClientEvent", (_, _) =>
        {
            received = true;
            return Task.CompletedTask;
        });

        await connection.SendCoreAsync("SendEvent", new object[]
        {
            new ClientEvent("no_such_event", JsonNode.Parse("{}")),
        });
        await connection.SendCoreAsync("SendEvent", new object[]
        {
            new ClientEvent("announce_event", JsonNode.Parse("""{"message":"x"}""")),
        });

        await Task.Delay(300);
        Assert.False(received);
        Assert.Empty(EventsPluginLog());
    }

    [Fact]
    public async Task Connection_InvalidToken_IsRejected()
    {
        await using var harness = await GatewayHarness.CreateAsync(token: "right-token");
        await using var connection = harness.BuildConnection(token: "wrong-token");

        await AssertConnectionRejectedAsync(connection);
        Assert.Empty(harness.Gateway.Terminals);
    }

    [Theory]
    [InlineData("", "user")]
    [InlineData("term-1", "")]
    public async Task Connection_MissingHandshakeParameter_IsRejected(string terminalId, string role)
    {
        await using var harness = await GatewayHarness.CreateAsync();
        await using var connection = harness.BuildConnection(terminalId: terminalId, role: role);

        await AssertConnectionRejectedAsync(connection);
        Assert.Empty(harness.Gateway.Terminals);
    }

    [Fact]
    public async Task Disconnect_RemovesTerminalFromRegistry()
    {
        await using var harness = await GatewayHarness.CreateAsync();
        var connection = await harness.ConnectAsync(terminalId: "ui-1");
        Assert.Single(harness.Gateway.Terminals);

        await connection.DisposeAsync();
        await WaitForAsync(() => harness.Gateway.Terminals.Count == 0);

        Assert.Empty(harness.Gateway.Terminals);
    }

    [Fact]
    public async Task InvokeOperation_CallerContext_IsSet()
    {
        await using var harness = await GatewayHarness.CreateAsync();
        TerminalInfo? captured = null;
        harness.Gateway.RegisterOperation<string, string>("whoami", (ctx, _, _) =>
        {
            captured = ctx.Caller;
            return Task.FromResult(ctx.Caller.TerminalId);
        });

        await using var connection = await harness.ConnectAsync(terminalId: "my-ui", role: "admin");
        var response = await connection.InvokeCoreAsync<OperationResponse>("InvokeOperation", new object[]
        {
            new OperationRequest("whoami", null),
        });

        Assert.True(response.Success);
        Assert.Equal("my-ui", response.Payload!.GetValue<string>());
        Assert.NotNull(captured);
        Assert.Equal("my-ui", captured!.TerminalId);
        Assert.Equal("admin", captured.Role);
        Assert.Equal(connection.ConnectionId, captured.ConnectionId);
    }

    [Fact]
    public async Task PluginLoad_ScansEventRouters_IntoRegistry()
    {
        await using var harness = await GatewayHarness.CreateAsync();
        LoadEventsPlugin(harness);

        Assert.True(harness.Events.TryGetEventRouter("report_event", out var reportType, out var fromClients));
        Assert.Equal("Momoka.Core.Tests.Plugins.Events.ReportEvent", reportType.FullName);
        Assert.True(fromClients);

        Assert.True(harness.Events.TryGetEventRouter("announce_event", out var announceType, out _));
        Assert.Equal("Momoka.Core.Tests.Plugins.Events.AnnounceEvent", announceType.FullName);
    }

    [Fact]
    public async Task PluginLoad_DuplicateEventId_FailsFast()
    {
        await using var harness = await GatewayHarness.CreateAsync();
        var loader = harness.App.Services.GetRequiredService<PluginLoader>();

        var ex = Assert.Throws<InvalidOperationException>(() => loader.Load(RouterBadPath()));
        Assert.Contains("dup_event", ex.Message);
    }

    [Fact]
    public async Task EventHub_Publish_WritesAuditLog()
    {
        var logs = new CapturingLoggerProvider();
        await using var harness = await GatewayHarness.CreateAsync(logs: logs);

        await harness.Events.InvokeAsync(new LocalPlainEvent("x"));

        Assert.Contains(logs.Messages, m => m.Contains("published") && m.Contains("LocalPlainEvent"));
    }

    private static Plugin LoadEventsPlugin(GatewayHarness harness)
    {
        var loader = harness.App.Services.GetRequiredService<PluginLoader>();
        Plugin plugin = loader.Load(EventsPluginPath());
        Assert.True(loader.EnableAsync(plugin));
        return plugin;
    }

    private static async Task AssertConnectionRejectedAsync(HubConnection connection)
    {
        var closed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        connection.Closed += _ =>
        {
            closed.TrySetResult();
            return Task.CompletedTask;
        };

        try
        {
            await connection.StartAsync();
        }
        catch
        {
            // 服务器可能在握手期直接终止，StartAsync 抛异常亦可接受
        }

        await closed.Task.WaitAsync(TimeSpan.FromSeconds(10));
    }

    private static void ResetEventsPluginLog()
    {
        Type type = LoadEventsPluginType();
        type.GetMethod("Reset", BindingFlags.Public | BindingFlags.Static)!.Invoke(null, null);
    }

    private static IReadOnlyList<string> EventsPluginLog()
    {
        Type type = LoadEventsPluginType();
        return (IReadOnlyList<string>)type.GetProperty("Log", BindingFlags.Public | BindingFlags.Static)!
            .GetValue(null)!;
    }

    private static Type LoadEventsPluginType()
        => AssemblyLoadContext.Default.LoadFromAssemblyPath(EventsPluginPath())
            .GetType("Momoka.Core.Tests.Plugins.Events.EventsPlugin")
            ?? throw new InvalidOperationException("EventsPlugin type was not found.");

    private static string EventsPluginPath()
        => Path.Combine(AppContext.BaseDirectory, "Plugins", "events", "PluginEvents.dll");

    private static string RouterBadPath()
        => Path.Combine(AppContext.BaseDirectory, "Plugins", "routerbad", "PluginRouterBad.dll");

    private static async Task WaitForAsync(Func<bool> condition, int timeoutMs = 5000)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (!condition())
        {
            if (DateTime.UtcNow >= deadline)
            {
                throw new TimeoutException("Condition was not met within the timeout.");
            }

            await Task.Delay(20);
        }
    }

    private sealed record SetLightRequest(int Brightness, string RoomName);

    private sealed record SetLightResponse(string Message);

    private sealed record LocalPlainEvent(string Value);

    private sealed class GatewayHarness : IAsyncDisposable
    {
        private readonly WebApplication _app;

        public GatewayHarness(WebApplication app, Gateway gateway, EventHub events)
        {
            _app = app;
            Gateway = gateway;
            Events = events;
        }

        public WebApplication App => _app;

        public TestServer Server => _app.GetTestServer();

        public Gateway Gateway { get; }

        public EventHub Events { get; }

        public static async Task<GatewayHarness> CreateAsync(
            string token = "test-token",
            CapturingLoggerProvider? logs = null)
        {
            var builder = WebApplication.CreateBuilder();
            builder.WebHost.UseTestServer();
            builder.Logging.ClearProviders();
            if (logs is not null)
            {
                builder.Logging.AddFilter((_, _) => true);
                builder.Logging.AddProvider(logs);
            }

            builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Gateway:Token"] = token,
            });

            GatewayHostBuilder.ConfigureGatewayServices(builder.Services, builder.Configuration);

            var app = builder.Build();
            app.MapHub<GatewayHub>("/hubs/gateway");
            await app.StartAsync();

            var gateway = app.Services.GetRequiredService<Gateway>();
            var events = app.Services.GetRequiredService<EventHub>();
            return new GatewayHarness(app, gateway, events);
        }

        public HubConnection BuildConnection(
            string terminalId = "term-1",
            string role = "user",
            string? token = null)
        {
            string url =
                $"http://localhost/hubs/gateway?terminalId={Uri.EscapeDataString(terminalId)}" +
                $"&role={Uri.EscapeDataString(role)}&token={Uri.EscapeDataString(token ?? "test-token")}";

            return new HubConnectionBuilder()
                .WithUrl(url, options =>
                {
                    options.HttpMessageHandlerFactory = _ => Server.CreateHandler();
                    options.Transports = HttpTransportType.LongPolling;
                })
                .AddJsonProtocol(options => options.PayloadSerializerOptions = GatewayJson.Options)
                .Build();
        }

        public async Task<HubConnection> ConnectAsync(
            string terminalId = "term-1",
            string role = "user",
            string? token = null)
        {
            HubConnection connection = BuildConnection(terminalId, role, token);
            await connection.StartAsync();
            await WaitForAsync(() => Gateway.GetTerminal(connection.ConnectionId!) is not null);
            return connection;
        }

        public async ValueTask DisposeAsync()
        {
            await _app.DisposeAsync();
        }
    }

    private sealed class CapturingLoggerProvider : ILoggerProvider
    {
        private readonly List<string> _messages = new();

        public IReadOnlyList<string> Messages
        {
            get
            {
                lock (_messages)
                {
                    return _messages.ToList();
                }
            }
        }

        public ILogger CreateLogger(string categoryName) => new CaptureLogger(this);

        public void Dispose()
        {
        }

        private void Add(string message)
        {
            lock (_messages)
            {
                _messages.Add(message);
            }
        }

        private sealed class CaptureLogger : ILogger
        {
            private readonly CapturingLoggerProvider _provider;

            public CaptureLogger(CapturingLoggerProvider provider)
            {
                _provider = provider;
            }

            public IDisposable? BeginScope<TState>(TState state)
                where TState : notnull
                => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(
                LogLevel logLevel,
                EventId eventId,
                TState state,
                Exception? exception,
                Func<TState, Exception?, string> formatter)
                => _provider.Add(formatter(state, exception));
        }
    }
}
