using Microsoft.Extensions.Logging;

namespace Momoka.Core.Plugins;

/// <summary>
/// 插件抽象基类：包含自身 <see cref="Info"/>（PluginInfo，含 manifest 身份与运行时状态），
/// 宿主能力经共享 <see cref="PluginService"/> 注入（统一管理服务注册表 / 事件中心 / 日志工厂 /
/// 插件根目录）；插件专属日志器 / 目录 / 配置按自身名称派生。
/// 插件构造器须轻量无副作用；业务服务用服务定位（<c>Plugin.Services</c>）获取。
/// </summary>
public abstract class CorePlugin : IPlugin
{
    /// <summary>插件生命周期状态机：Discovered → Loaded → Started → Stopped / Failed。</summary>
    public enum PluginState
    {
        /// <summary>已从程序集发现（manifest 已解析）。</summary>
        Discovered = 0,

        /// <summary>已实例化并 Load（<c>OnLoad</c> 已运行）。</summary>
        Loaded = 1,

        /// <summary>已 <c>StartAsync</c>。</summary>
        Started = 2,

        /// <summary>已 <c>StopAsync</c>。</summary>
        Stopped = 3,

        /// <summary>生命周期失败（启动失败回滚或停止失败）。</summary>
        Failed = 4,
    }

    private PluginInfo? _info;
    private PluginService? _pluginService;
    private ILogger? _logger;

    /// <summary>插件信息（manifest + 运行时状态，注入时回填）。未注入前访问抛 <see cref="InvalidOperationException"/>。</summary>
    public PluginInfo Info => _info
        ?? throw new InvalidOperationException("Plugin host has not been injected yet.");

    /// <inheritdoc />
    public string Name => Info.Name;

    /// <inheritdoc />
    public string Version => Info.Version;

    /// <summary>宿主能力束（共享实例，注入）。未注入前访问抛 <see cref="InvalidOperationException"/>。</summary>
    protected PluginService Plugin => _pluginService
        ?? throw new InvalidOperationException("Plugin host has not been injected yet.");

    /// <summary>插件专属日志器（类别 = 插件名，懒创建）。</summary>
    protected ILogger Logger => _logger ??= Plugin.LoggerFactory.CreateLogger(Name);

    /// <summary>初始化钩子：宿主能力注入后、StartAsync 前调用一次。</summary>
    protected virtual void OnLoad()
    {
    }

    /// <summary>插件可写目录（Plugins/&lt;Name&gt;/，首次访问自动创建；目录编排由插件自行决定）。</summary>
    protected DirectoryInfo GetPluginFolder()
    {
        var folder = new DirectoryInfo(Path.Combine(Plugin.PluginsDirectory.FullName, Name));
        folder.Create();
        return folder;
    }

    /// <summary>插件配置文件（Plugins/&lt;Name&gt;/config.toml，首次访问自动创建）。</summary>
    protected FileInfo GetPluginConfig()
    {
        var file = new FileInfo(Path.Combine(Plugin.PluginsDirectory.FullName, Name, "config.toml"));
        if (!file.Exists)
        {
            file.Directory?.Create();
            file.Create().Dispose();
        }

        return file;
    }

    /// <summary>
    /// 宿主注入插件信息与共享能力束。仅供 <see cref="PluginLoader"/> 调用。
    /// </summary>
    internal void InjectHost(PluginInfo info, PluginService pluginService)
    {
        ArgumentNullException.ThrowIfNull(info);
        ArgumentNullException.ThrowIfNull(pluginService);
        _info = info;
        _pluginService = pluginService;
    }

    /// <summary>
    /// 加载插件（非虚）：重复 Load 抛 <see cref="InvalidOperationException"/>（以
    /// <see cref="Info"/> 上的 <see cref="PluginState"/> 守卫），随后调用 <see cref="OnLoad"/>。
    /// 仅供 <see cref="PluginLoader"/> 调用。
    /// </summary>
    internal void Load()
    {
        if (Info.State != PluginState.Discovered)
        {
            throw new InvalidOperationException($"Plugin '{Name}' has already been loaded.");
        }

        Info.State = PluginState.Loaded;
        OnLoad();
    }

    /// <inheritdoc />
    public abstract Task StartAsync(CancellationToken cancellationToken);

    /// <inheritdoc />
    public abstract Task StopAsync(CancellationToken cancellationToken);
}
