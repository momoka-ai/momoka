using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Momoka.Core.Events;

namespace Momoka.Core;

/// <summary>
/// 宿主 DI 接线（Program.cs 与测试自建内联 WebApplication 共用，避免两份接线漂移）：
/// SignalR（AddSignalR + snake_case JSON 协议）+ 核心单例（EventHub / Gateway）。
/// Gateway 经工厂构建（token 直接读配置）。插件运行时接线在插件子系统重建后追加。
/// </summary>
internal static class GatewayHostBuilder
{
    /// <summary>注册网关宿主服务到 <paramref name="services"/>（基于 <paramref name="configuration"/> 的 Gateway 节）。</summary>
    public static void ConfigureGatewayServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddSignalR().AddJsonProtocol(options =>
            options.PayloadSerializerOptions = GatewayJson.Options);

        services.AddSingleton<EventHub>();
        services.AddSingleton(new Gateway(configuration["Gateway:Token"]));
    }
}
