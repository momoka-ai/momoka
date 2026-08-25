using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Momoka.Core.Events;
using Momoka.Core.Plugins;

namespace Momoka.Core;

/// <summary>
/// 插件宿主入口：Generic Host 底座（后续换 WebApplication 底座）——
/// 启动插件（扫描/排序/校验/注入）→ 打印插件图 → 运行 → 逆序停止。
/// 插件根目录硬编码于基目录（可经配置 Plugins:BaseDirectory 覆写）下的 Plugins；
/// 插件启停由 Core 自带配置（Plugins:Disabled）管理。
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
        IReadOnlySet<string> disabled = ReadDisabledPlugins(builder.Configuration);

        try
        {
            await loader.StartAsync(disabled);
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

    private static HashSet<string> ReadDisabledPlugins(ConfigurationManager configuration)
    {
        string[]? names = configuration.GetSection("Plugins").GetSection("Disabled").Get<string[]>();
        return names is null
            ? new HashSet<string>(StringComparer.Ordinal)
            : new HashSet<string>(names, StringComparer.Ordinal);
    }
}
