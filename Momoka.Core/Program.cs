using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Momoka.Core.Plugins;

namespace Momoka.Core;

/// <summary>
/// 插件宿主入口：WebApplication 底座（SignalR 网关）——扫描插件根目录 → 逐插件 Load（实例化 +
/// 行为扫描注册）→ EnableAsync（按依赖图依序启用）→ 运行 → 逆序停用。
/// 插件根目录硬编码于基目录（可经配置 Plugins:BaseDirectory 覆写）下的 Plugins。
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

        var loader = app.Services.GetRequiredService<PluginLoader>();
        var pluginService = app.Services.GetRequiredService<PluginService>();

        try
        {
            foreach (string file in PluginLoader.GetPluginFiles(pluginService.PluginsDirectory.FullName))
            {
                if (PluginLoader.GetPluginInfo(file) is null)
                {
                    continue; // 依赖库（无 manifest）跳过
                }

                loader.Load(file);
            }

            await loader.EnableAsync();
            await app.RunAsync();
        }
        finally
        {
            await loader.DisableAsync();
        }
    }
}
