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
/// 读插件目录配置 → 启动插件（扫描/排序/校验/注入）→ 打印插件图 → 运行 → 逆序停止。
/// </summary>
public static class Program
{
    public static async Task Main(string[] args)
    {
        var builder = Host.CreateApplicationBuilder(args);

        builder.Logging.AddConsole();
        builder.Services.AddSingleton<IServiceRegistry, ServiceRegistry>();
        builder.Services.AddSingleton<IEventBus, EventBus>();
        builder.Services.AddSingleton(sp => new PluginLoader(
            CreateLoaderOptions(builder.Configuration),
            sp.GetRequiredService<IServiceRegistry>(),
            sp.GetRequiredService<IEventBus>(),
            sp.GetRequiredService<ILoggerFactory>()));

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

    private static PluginLoaderOptions CreateLoaderOptions(IConfiguration configuration)
    {
        string baseDir = AppContext.BaseDirectory;
        return new PluginLoaderOptions(
            new DirectoryInfo(ResolvePath(configuration["Plugins:PluginDirectory"], Path.Combine(baseDir, "Plugins"))),
            new DirectoryInfo(ResolvePath(configuration["Plugins:ConfigDirectory"], Path.Combine(baseDir, "Config"))),
            new DirectoryInfo(ResolvePath(configuration["Plugins:DataDirectory"], Path.Combine(baseDir, "Data"))));
    }

    private static string ResolvePath(string? configured, string fallback)
        => string.IsNullOrWhiteSpace(configured) ? fallback : configured;
}
