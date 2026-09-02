using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Momoka.Core.Commands;
using Momoka.Core.Events;
using Momoka.Core.Services;

namespace Momoka.Core.Plugins;

/// <summary>
/// 插件声明面（宿主注入身份与环境后，经 plugin.toml 声明的静态 Build 入口填充）：
/// 插件只做声明、不控制生命周期——生命周期完全由宿主（加载/启用/停用）接管。
/// 声明项：服务（<see cref="AddService{T}"/>，写入 <see cref="Service{T}"/> 泛型注册表，声明记录
/// 供 [ServiceInjection] 注入与反注册）／指令（<see cref="Commands"/>）／事件监听器
/// （<see cref="EventHandlers"/>）。指令与监听器是 Core 管理的回调对象，[ServiceInjection]
/// 注入目标仅限服务提供者。专属日志器 / 目录 / 配置文件按自身名称派生。
/// </summary>
public sealed class Plugin
{
    private readonly string _pluginsRoot;
    private readonly ILogger _logger;

    /// <summary>创建插件声明面。插件根目录（基目录下 Plugins）由宿主注入；日志工厂缺省取 NullLogger。</summary>
    public Plugin(PluginInfo info, string pluginsRootDirectory, ILoggerFactory? loggerFactory = null)
    {
        ArgumentNullException.ThrowIfNull(info);
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginsRootDirectory);
        Info = info;
        _pluginsRoot = Path.GetFullPath(pluginsRootDirectory);
        _logger = (loggerFactory ?? NullLoggerFactory.Instance).CreateLogger(info.Name);
    }

    /// <summary>插件信息（manifest，身份）。</summary>
    public PluginInfo Info { get; }

    /// <summary>插件名（全局唯一，与 manifest.name 一致）。</summary>
    public string Name => Info.Name;

    /// <summary>插件版本（与 manifest.version 一致）。</summary>
    public string Version => Info.Version;

    /// <summary>插件专属日志器（类别 = 插件名）。</summary>
    public ILogger Logger => _logger;

    /// <summary>已声明的指令（Core 管理；注册进指令系统由宿主完成）。</summary>
    public IList<Command> Commands { get; } = new List<Command>();

    /// <summary>已声明的事件监听器实例（实现 ≥1 个 <c>IEventHandler&lt;TEvent&gt;</c>，宿主启用时统一注册）。</summary>
    public IList<object> EventHandlers { get; } = new List<object>();

    /// <summary>本插件声明的服务注册记录（[ServiceInjection] 注入目标仅限服务提供者；
    /// 宿主启停时按记录反注册/重建）。</summary>
    internal IList<ServiceProviderRegistration> ServiceProviders { get; } = new List<ServiceProviderRegistration>();

    /// <summary>一次服务声明：接口类型（Service&lt;T&gt; 的 T）+ 提供商实例。</summary>
    internal sealed record ServiceProviderRegistration(Type ServiceType, object Provider);

    /// <summary>
    /// 声明服务提供商：立即写入 <see cref="Service{T}"/> 泛型注册表，来源 = 本插件，并记录声明供注入与反注册。
    /// 默认先到先得（后续同类型注册成为可选提供商）；<paramref name="overwrite"/> = true 时显式替换当前提供商。
    /// </summary>
    public Plugin AddService<T>(T provider, bool overwrite = false)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(provider);
        if (overwrite)
        {
            Service<T>.Register(provider, this);
        }
        else
        {
            Service<T>.TryRegister(provider, this);
        }

        ServiceProviders.Add(new ServiceProviderRegistration(typeof(T), provider));
        return this;
    }

    /// <summary>声明指令。</summary>
    public Plugin AddCommand(Command command)
    {
        ArgumentNullException.ThrowIfNull(command);
        Commands.Add(command);
        return this;
    }

    /// <summary>声明事件监听器（实现 ≥1 个 <c>IEventHandler&lt;TEvent&gt;</c>；宿主启用时统一注册进事件总线）。</summary>
    public Plugin AddEventHandler(object listener)
    {
        ArgumentNullException.ThrowIfNull(listener);
        EventHandlers.Add(listener);
        return this;
    }

    /// <summary>插件可写目录（Plugins/&lt;Name&gt;/，首次访问自动创建；目录编排由插件自行决定）。</summary>
    public DirectoryInfo GetPluginFolder()
    {
        var folder = new DirectoryInfo(Path.Combine(_pluginsRoot, Name));
        folder.Create();
        return folder;
    }

    /// <summary>插件配置文件（Plugins/&lt;Name&gt;/config.toml，首次访问自动创建）。</summary>
    public FileInfo GetPluginConfig()
    {
        var file = new FileInfo(Path.Combine(_pluginsRoot, Name, "config.toml"));
        if (!file.Exists)
        {
            file.Directory?.Create();
            file.Create().Dispose();
        }

        return file;
    }
}
