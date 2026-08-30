using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Momoka.Core.Behaviors;
using Momoka.Core.Plugins;

namespace Momoka.Core;

/// <summary>
/// 宿主 DI 接线（Program.cs 与测试自建内联 WebApplication 共用，避免两份接线漂移）：
/// SignalR（AddSignalR + snake_case JSON 协议）+ 核心单例（ServiceRegistry / EventHub / Gateway /
/// PluginService / PluginLoader）。EventHub 的 wire-sender 经 DI 工厂闭包注入（无可变 setter），
/// 延迟解析 <see cref="Gateway"/> 以打破构造环；Gateway 经工厂构建（token 直接读配置）。
/// </summary>
internal static class GatewayHostBuilder
{
    /// <summary>注册网关宿主服务到 <paramref name="services"/>（基于 <paramref name="configuration"/> 的 Gateway 节与插件目录）。</summary>
    public static void ConfigureGatewayServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddSignalR().AddJsonProtocol(options =>
            options.PayloadSerializerOptions = GatewayJson.Options);

        services.AddSingleton<ServiceRegistry>();
        services.AddSingleton(sp => CreateEventHub(sp));
        services.AddSingleton(sp => new Gateway(
            sp.GetRequiredService<EventHub>(),
            sp.GetService<IHubContext<GatewayHub, IGatewayClient>>(),
            sp.GetService<ILogger<Gateway>>(),
            configuration["Gateway:Token"]));
        services.AddSingleton(sp => new PluginService(
            sp.GetRequiredService<ServiceRegistry>(),
            sp.GetRequiredService<EventHub>(),
            sp.GetRequiredService<ILoggerFactory>(),
            ReadBaseDirectory(configuration),
            sp.GetRequiredService<Gateway>()));
        services.AddSingleton(sp => new PluginLoader(sp.GetRequiredService<PluginService>()));
    }

    private static EventHub CreateEventHub(IServiceProvider sp)
    {
        return new EventHub(
            sp.GetRequiredService<ILogger<EventHub>>(),
            (eventId, payload) => sp.GetRequiredService<Gateway>().BroadcastClientEvent(eventId, payload));
    }

    private static string? ReadBaseDirectory(IConfiguration configuration)
    {
        string? configured = configuration["Plugins:BaseDirectory"];
        return string.IsNullOrWhiteSpace(configured) ? null : configured;
    }
}
