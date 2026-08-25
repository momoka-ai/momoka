using Microsoft.Extensions.Logging;
using Momoka.Core.Events;
using Momoka.Core.Registry;

namespace Momoka.Core.Plugins;

/// <summary>
/// 插件宿主能力束。两个形态：
/// <list type="bullet">
/// <item><description><b>宿主级</b>（public 构造，DI 注册）：统一持有服务注册表 / 事件总线 / 日志工厂
/// 与三个运行时目录（Plugins / Config / Data，硬编码于基目录之下），经 <see cref="ForPlugin"/> 派生插件级。</description></item>
/// <item><description><b>插件级</b>（<see cref="ForPlugin"/> 派生，注入 <see cref="CorePlugin"/>）：绑定插件名与专属
/// 日志器；数据目录与配置文件按需即时生成（首次访问自动创建）。</description></item>
/// </list>
/// </summary>
public sealed class PluginService
{
    private readonly string? _pluginName;
    private readonly ILogger? _pluginLogger;
    private readonly DirectoryInfo? _pluginsDataDirectory;
    private readonly DirectoryInfo? _pluginsConfigDirectory;

    /// <summary>创建宿主级能力束。目录位于 <paramref name="baseDirectory"/>（缺省
    /// <see cref="AppContext.BaseDirectory"/>）下的 Plugins / Config / Data。</summary>
    public PluginService(
        IServiceRegistry services,
        IEventBus events,
        ILoggerFactory loggerFactory,
        string? baseDirectory = null)
    {
        Services = services ?? throw new ArgumentNullException(nameof(services));
        Events = events ?? throw new ArgumentNullException(nameof(events));
        LoggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));

        string baseDir = baseDirectory ?? AppContext.BaseDirectory;
        PluginsDirectory = new DirectoryInfo(Path.Combine(baseDir, "Plugins"));
        ConfigDirectory = new DirectoryInfo(Path.Combine(baseDir, "Config"));
        DataDirectory = new DirectoryInfo(Path.Combine(baseDir, "Data"));
    }

    private PluginService(string pluginName, PluginService host, ILogger logger)
    {
        _pluginName = pluginName;
        Services = host.Services;
        Events = host.Events;
        LoggerFactory = host.LoggerFactory;
        PluginsDirectory = host.PluginsDirectory;
        ConfigDirectory = host.ConfigDirectory;
        DataDirectory = host.DataDirectory;
        _pluginLogger = logger;
        _pluginsDataDirectory = new DirectoryInfo(Path.Combine(host.DataDirectory.FullName, "Plugins"));
        _pluginsConfigDirectory = new DirectoryInfo(Path.Combine(host.ConfigDirectory.FullName, "Plugins"));
    }

    /// <summary>插件间服务发现表（宿主级共享）。</summary>
    public IServiceRegistry Services { get; }

    /// <summary>强类型事件总线（宿主级共享）。</summary>
    public IEventBus Events { get; }

    /// <summary>日志工厂（宿主级；插件专属日志器经 <see cref="ForPlugin"/> 派生）。</summary>
    public ILoggerFactory LoggerFactory { get; }

    /// <summary>插件目录（&lt;base&gt;/Plugins）。</summary>
    public DirectoryInfo PluginsDirectory { get; }

    /// <summary>宿主配置目录（&lt;base&gt;/Config）。</summary>
    public DirectoryInfo ConfigDirectory { get; }

    /// <summary>插件数据目录（&lt;base&gt;/Data）。</summary>
    public DirectoryInfo DataDirectory { get; }

    /// <summary>插件名（仅插件级）。</summary>
    public string Name => _pluginName
        ?? throw new InvalidOperationException("Host-level PluginService does not belong to a single plugin.");

    /// <summary>插件专属日志器（类别 = 插件名，仅插件级）。</summary>
    public ILogger Logger => _pluginLogger
        ?? throw new InvalidOperationException("Host-level PluginService has no plugin logger.");

    /// <summary>插件可写数据目录（Data/Plugins/&lt;name&gt;/，首次访问自动创建，仅插件级）。</summary>
    public DirectoryInfo GetPluginFolder()
    {
        var baseDirectory = _pluginsDataDirectory
            ?? throw new InvalidOperationException("Host-level PluginService has no plugin data directory.");
        var folder = new DirectoryInfo(Path.Combine(baseDirectory.FullName, Name));
        folder.Create();
        return folder;
    }

    /// <summary>插件可写配置文件（Config/Plugins/&lt;name&gt;.toml，首次访问自动创建，仅插件级）。</summary>
    public FileInfo GetPluginConfig()
    {
        var baseDirectory = _pluginsConfigDirectory
            ?? throw new InvalidOperationException("Host-level PluginService has no plugin config directory.");
        var file = new FileInfo(Path.Combine(baseDirectory.FullName, Name + ".toml"));
        if (!file.Exists)
        {
            file.Directory?.Create();
            file.Create().Dispose();
        }

        return file;
    }

    /// <summary>派生指定插件的插件级能力束（专属日志器 + 数据/配置目录）。</summary>
    internal PluginService ForPlugin(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return new PluginService(name, this, LoggerFactory.CreateLogger(name));
    }
}
