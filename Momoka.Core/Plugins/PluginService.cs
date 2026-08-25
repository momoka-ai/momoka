using Microsoft.Extensions.Logging;
using Momoka.Core.Events;
using Momoka.Core.Registry;

namespace Momoka.Core.Plugins;

/// <summary>
/// 插件宿主能力束：宿主注入的唯一入口，统一管理服务注册表 / 事件总线 / 日志器；
/// 插件数据目录与配置文件**按需即时生成**（首次访问自动创建），不预先存储引用。
/// </summary>
public sealed class PluginService
{
    private readonly DirectoryInfo _pluginsDataDirectory;
    private readonly DirectoryInfo _pluginsConfigDirectory;

    internal PluginService(
        string name,
        IServiceRegistry services,
        IEventBus events,
        ILogger logger,
        DirectoryInfo pluginsDataDirectory,
        DirectoryInfo pluginsConfigDirectory)
    {
        Name = name;
        Services = services;
        Events = events;
        Logger = logger;
        _pluginsDataDirectory = pluginsDataDirectory;
        _pluginsConfigDirectory = pluginsConfigDirectory;
    }

    /// <summary>插件名。</summary>
    public string Name { get; }

    /// <summary>插件间服务发现表（宿主级共享）。</summary>
    public IServiceRegistry Services { get; }

    /// <summary>强类型事件总线（宿主级共享）。</summary>
    public IEventBus Events { get; }

    /// <summary>插件专属日志器（类别 = 插件名）。</summary>
    public ILogger Logger { get; }

    /// <summary>插件可写数据目录（Data/Plugins/&lt;name&gt;/，首次访问自动创建）。</summary>
    public DirectoryInfo GetPluginFolder()
    {
        var folder = new DirectoryInfo(Path.Combine(_pluginsDataDirectory.FullName, Name));
        folder.Create();
        return folder;
    }

    /// <summary>插件可写配置文件（Config/Plugins/&lt;name&gt;.toml，首次访问自动创建）。</summary>
    public FileInfo GetPluginConfig()
    {
        var file = new FileInfo(Path.Combine(_pluginsConfigDirectory.FullName, Name + ".toml"));
        if (!file.Exists)
        {
            file.Directory?.Create();
            file.Create().Dispose();
        }

        return file;
    }
}
