using Microsoft.Extensions.Logging;

namespace Momoka.Core.Plugins;

/// <summary>
/// 插件抽象基类：包含自身 <see cref="Info"/>（manifest 身份），宿主能力经共享
/// <see cref="PluginService"/> 注入（<see cref="Host"/>）；专属日志器 / 目录 / 配置按自身名称派生。
/// 生命周期由 <see cref="PluginLoader"/> 驱动：Load 后即就绪；EnableAsync 调用
/// <see cref="OnEnable"/>（注册服务/订阅事件）；DisableAsync 调用 <see cref="OnDisable"/>
/// （清理由插件自行完成）。插件构造器须轻量无副作用。
/// </summary>
public abstract class Plugin
{
    private PluginInfo? _info;
    private PluginService? _host;
    private ILogger? _logger;

    /// <summary>插件信息（manifest，注入时回填）。未注入前访问抛 <see cref="InvalidOperationException"/>。</summary>
    public PluginInfo Info => _info
        ?? throw new InvalidOperationException("Plugin host has not been injected yet.");

    /// <summary>插件名（全局唯一，与 manifest.name 一致）。</summary>
    public string Name => Info.Name;

    /// <summary>插件版本（与 manifest.version 一致）。</summary>
    public string Version => Info.Version;

    /// <summary>生命周期状态（由 <see cref="PluginLoader"/> 推进）。</summary>
    public PluginState State { get; internal set; } = PluginState.Loaded;

    /// <summary>宿主能力束（共享实例，注入）。未注入前访问抛 <see cref="InvalidOperationException"/>。</summary>
    protected PluginService Host => _host
        ?? throw new InvalidOperationException("Plugin host has not been injected yet.");

    /// <summary>插件专属日志器（类别 = 插件名，懒创建）。</summary>
    protected ILogger Logger => _logger ??= Host.LoggerFactory.CreateLogger(Name);

    /// <summary>启用钩子：<see cref="PluginLoader"/> 在 EnableAsync 时调用一次（注册服务 / 订阅事件等初始化）。</summary>
    public virtual void OnEnable()
    {
    }

    /// <summary>停用钩子：<see cref="PluginLoader"/> 在 DisableAsync 时调用一次（注销服务 / 退订等清理，由插件自行完成）。</summary>
    public virtual void OnDisable()
    {
    }

    /// <summary>插件可写目录（Plugins/&lt;Name&gt;/，首次访问自动创建；目录编排由插件自行决定）。</summary>
    protected DirectoryInfo GetPluginFolder()
    {
        var folder = new DirectoryInfo(Path.Combine(Host.PluginsDirectory.FullName, Name));
        folder.Create();
        return folder;
    }

    /// <summary>插件配置文件（Plugins/&lt;Name&gt;/config.toml，首次访问自动创建）。</summary>
    protected FileInfo GetPluginConfig()
    {
        var file = new FileInfo(Path.Combine(Host.PluginsDirectory.FullName, Name, "config.toml"));
        if (!file.Exists)
        {
            file.Directory?.Create();
            file.Create().Dispose();
        }

        return file;
    }

    /// <summary>
    /// 提取本插件打包的嵌入资源流（资源路径为程序集内嵌名，如 <c>Momoka.Home.plugin.toml</c>）；
    /// 未找到返回 null。实现为按插件自身程序集查询 <see cref="System.Reflection.Assembly.GetManifestResourceStream"/>。
    /// </summary>
    protected Stream? GetPluginResource(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        return GetType().Assembly.GetManifestResourceStream(path);
    }

    /// <summary>
    /// 宿主注入插件信息与共享能力束。仅供 <see cref="PluginLoader"/> 调用。
    /// </summary>
    internal void InjectHost(PluginInfo info, PluginService host)
    {
        ArgumentNullException.ThrowIfNull(info);
        ArgumentNullException.ThrowIfNull(host);
        _info = info;
        _host = host;
    }
}
