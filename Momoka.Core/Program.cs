using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Logging;

namespace Momoka.Core;

/// <summary>
/// 宿主入口：WebApplication 底座（SignalR 网关）——目前仅承载网关设施；
/// 插件扫描 / 启停编排在插件子系统（PluginContext）重建后接入。
/// </summary>
public static class Program
{
    public static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        builder.Logging.AddConsole();
        GatewayHostBuilder.ConfigureGatewayServices(builder.Services, builder.Configuration);

        using var app = builder.Build();

        app.MapHub<GatewayHub>("/hubs/gateway");

        await app.RunAsync();
    }
}
