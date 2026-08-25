using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Momoka.Core.Events;
using Momoka.Core.Plugins;
using Momoka.Core.Registry;

namespace Momoka.Core;

/// <summary>
/// 插件宿主入口：Generic Host 底座（后续换 WebApplication 底座）——
/// 启动插件（扫描/排序/校验/注入）→ 打印插件图 → 运行 → 逆序停止。
/// 插件目录硬编码于基目录（可经配置 Plugins:BaseDirectory 覆写）下的
/// Plugins / Config / Data。
/// </summary>
public static class Program
{
    public static async Task Main(string[] args)
    {
        var builder = Host.CreateApplicationBuilder(args);

        builder.Logging.AddConsole();
        builder.Services.AddSingleton<IServiceRegistry, ServiceRegistry>();
        builder.Services.AddSingleton<IEventBus, EventBus>();
        builder.Services.AddSingleton(sp => new PluginService(
            sp.GetRequiredService<IServiceRegistry>(),
            sp.GetRequiredService<IEventBus>(),
            sp.GetRequiredService<ILoggerFactory>(),
            ReadBaseDirectory(builder.Configuration)));
        builder.Services.AddSingleton(sp => new PluginLoader(sp.GetRequiredService<PluginService>()));

        using var host = builder.Build();
        var loader = host.Services.GetRequiredService<PluginLoader>();

        try
        {
            await loader.StartAsync();
            await host.RunAsync();
        }
        finally
        {
            await loader.StopAsync();
        }
    }

    private static string? ReadBaseDirectory(IConfiguration configuration)
    {
        string? configured = configuration["Plugins:BaseDirectory"];
        return string.IsNullOrWhiteSpace(configured) ? null : configured;
    }
}
