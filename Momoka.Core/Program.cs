using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Momoka.Core.Events;
using Momoka.Core.Plugins;

namespace Momoka.Core;

/// <summary>
/// 插件宿主入口：Generic Host 底座（后续换 WebApplication 底座）——
/// 扫描插件根目录 → 逐插件 Load（实例化）→ EnableAsync（按依赖图依序启用）→ 运行 → 逆序停用。
/// 插件根目录硬编码于基目录（可经配置 Plugins:BaseDirectory 覆写）下的 Plugins。
/// </summary>
public static class Program
{
    public static async Task Main(string[] args)
    {
        var builder = Host.CreateApplicationBuilder(args);

        builder.Logging.AddConsole();
        builder.Services.AddSingleton<ServiceRegistry>();
        builder.Services.AddSingleton<EventHub>();
        builder.Services.AddSingleton(sp => new PluginService(
            sp.GetRequiredService<ServiceRegistry>(),
            sp.GetRequiredService<EventHub>(),
            sp.GetRequiredService<ILoggerFactory>(),
            ReadBaseDirectory(builder.Configuration)));
        builder.Services.AddSingleton(sp => new PluginLoader(sp.GetRequiredService<PluginService>()));

        using var host = builder.Build();
        var loader = host.Services.GetRequiredService<PluginLoader>();
        var pluginService = host.Services.GetRequiredService<PluginService>();

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
            await host.RunAsync();
        }
        finally
        {
            await loader.DisableAsync();
        }
    }

    private static string? ReadBaseDirectory(IConfiguration configuration)
    {
        string? configured = configuration["Plugins:BaseDirectory"];
        return string.IsNullOrWhiteSpace(configured) ? null : configured;
    }
}
