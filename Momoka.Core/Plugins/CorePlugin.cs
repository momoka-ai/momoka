using Microsoft.Extensions.Logging;
using Momoka.Core.Events;
using Momoka.Core.Registry;

namespace Momoka.Core.Plugins;

/// <summary>
/// 插件抽象基类：宿主能力经单一 <see cref="PluginService"/> 注入（统一管理服务注册表 /
/// 事件总线 / 日志器）；插件数据目录与配置文件按需即时生成。插件构造器须轻量无副作用；
/// 业务服务用服务定位（<see cref="Services"/>）获取。
/// </summary>
public abstract class CorePlugin : IPlugin
{
    private PluginService? _pluginService;
    private PluginState _state = PluginState.Discovered;

    /// <inheritdoc />
    public string Name { get; internal set; } = null!;

    /// <inheritdoc />
    public string Version { get; internal set; } = null!;

    /// <summary>宿主能力束（注入）。未注入前访问抛 <see cref="InvalidOperationException"/>。</summary>
    protected PluginService Plugin => _pluginService
        ?? throw new InvalidOperationException("Plugin host has not been injected yet.");

    /// <summary>插件间服务发现表（宿主注入）。未注入前访问抛 <see cref="InvalidOperationException"/>。</summary>
    protected IServiceRegistry Services => Plugin.Services;

    /// <summary>强类型事件总线（宿主注入）。未注入前访问抛 <see cref="InvalidOperationException"/>。</summary>
    protected IEventBus Events => Plugin.Events;

    /// <summary>插件专属日志器（宿主注入，类别为插件名）。未注入前访问抛 <see cref="InvalidOperationException"/>。</summary>
    protected ILogger Logger => Plugin.Logger;

    /// <summary>订阅强类型事件；返回的令牌用于退订。宿主未注入时抛 <see cref="InvalidOperationException"/>。</summary>
    protected IDisposable Subscribe<TEvent>(Func<TEvent, Task> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        return Events.Subscribe(handler);
    }

    /// <summary>插件可写数据目录（Data/Plugins/&lt;name&gt;/，首次访问自动创建）。</summary>
    protected DirectoryInfo GetPluginFolder() => Plugin.GetPluginFolder();

    /// <summary>插件可写配置文件（Config/Plugins/&lt;name&gt;.toml，首次访问自动创建）。</summary>
    protected FileInfo GetPluginConfig() => Plugin.GetPluginConfig();

    /// <summary>初始化钩子：宿主能力注入后、StartAsync 前调用一次。</summary>
    protected virtual void OnLoad()
    {
    }

    /// <summary>
    /// 宿主注入能力束。仅供 <see cref="PluginLoader"/> 调用。
    /// </summary>
    internal void InjectHost(PluginService pluginService)
    {
        ArgumentNullException.ThrowIfNull(pluginService);
        _pluginService = pluginService;
    }

    /// <summary>
    /// 加载插件（非虚）：重复 Load 抛 <see cref="InvalidOperationException"/>（以
    /// <see cref="PluginState"/> 守卫），随后调用 <see cref="OnLoad"/>。
    /// 仅供 <see cref="PluginLoader"/> 调用。
    /// </summary>
    internal void Load()
    {
        if (_state != PluginState.Discovered)
        {
            throw new InvalidOperationException($"Plugin '{Name}' has already been loaded.");
        }

        _state = PluginState.Loaded;
        OnLoad();
    }

    /// <inheritdoc />
    public abstract Task StartAsync(CancellationToken cancellationToken);

    /// <inheritdoc />
    public abstract Task StopAsync(CancellationToken cancellationToken);
}
