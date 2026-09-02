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
using Xunit;

namespace Momoka.Core.Tests;

/// <summary>
/// 网关集成测试（自建内联 WebApplication + TestServer + SignalR 客户端，不走 Program.Main）：
/// 握手鉴权（clientId/role/token）/ 设备注册表（按 clientId 寻址、重连覆盖）/
/// 事件总线发布与事件审计日志。
/// </summary>
public sealed class GatewayTests
{
    [Fact]
    public async Task Connection_EstablishesAndRegistersDevice()
    {
        await using var harness = await GatewayHarness.CreateAsync();
        await using var connection = await harness.ConnectAsync(clientId: "my-ui", role: "admin");

        var device = Assert.Single(harness.Gateway.Clients);
        Assert.Equal("my-ui", device.ClientId);
        Assert.Equal("admin", device.Role);
        Assert.Equal(connection.ConnectionId, device.ConnectionId);
    }

    [Theory]
    [InlineData("", "user")]
    [InlineData("cli-1", "")]
    public async Task Connection_MissingHandshakeParameter_IsRejected(string clientId, string role)
    {
        await using var harness = await GatewayHarness.CreateAsync();
        await using var connection = harness.BuildConnection(clientId: clientId, role: role);

        await AssertConnectionRejectedAsync(connection);
        Assert.Empty(harness.Gateway.Clients);
    }

    [Fact]
    public async Task Connection_InvalidToken_IsRejected()
    {
        await using var harness = await GatewayHarness.CreateAsync();
        await using var connection = harness.BuildConnection(token: "wrong-token");

        await AssertConnectionRejectedAsync(connection);
        Assert.Empty(harness.Gateway.Clients);
    }

    [Fact]
    public async Task Disconnect_RemovesDeviceFromRegistry()
    {
        await using var harness = await GatewayHarness.CreateAsync();
        var connection = await harness.ConnectAsync(clientId: "ui-1");
        Assert.Single(harness.Gateway.Clients);

        await connection.DisposeAsync();
        await WaitForAsync(() => harness.Gateway.Clients.Count == 0);

        Assert.Empty(harness.Gateway.Clients);
    }

    [Fact]
    public async Task Reconnect_SameClientId_ReplacesConnectionPath()
    {
        await using var harness = await GatewayHarness.CreateAsync();
        var first = await harness.ConnectAsync(clientId: "ui-1");
        await first.DisposeAsync();
        await WaitForAsync(() => harness.Gateway.Clients.Count == 0);

        var second = await harness.ConnectAsync(clientId: "ui-1");

        var device = Assert.Single(harness.Gateway.Clients);
        Assert.Equal("ui-1", device.ClientId);
        Assert.Equal(second.ConnectionId, device.ConnectionId);
    }

    [Fact]
    public async Task EventHub_Publish_WritesAuditLog()
    {
        var logs = new CapturingLoggerProvider();
        await using var harness = await GatewayHarness.CreateAsync(logs: logs);

        await harness.Events.Publish(new LocalPlainEvent("x"));

        Assert.Contains(logs.Messages, m => m.Contains("published") && m.Contains("LocalPlainEvent"));
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

    private sealed record class LocalPlainEvent(string Value) : Event<LocalPlainEvent>;

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
            string clientId = "cli-1",
            string role = "user",
            string? token = null)
        {
            string url =
                $"http://localhost/hubs/gateway?clientId={Uri.EscapeDataString(clientId)}" +
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
            string clientId = "cli-1",
            string role = "user",
            string? token = null)
        {
            HubConnection connection = BuildConnection(clientId, role, token);
            await connection.StartAsync();
            await WaitForAsync(() => Gateway.GetClient(connection.ConnectionId!) is not null);
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
