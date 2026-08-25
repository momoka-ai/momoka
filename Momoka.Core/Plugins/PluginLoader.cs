using System.Reflection;
using Microsoft.Extensions.Logging;

namespace Momoka.Core.Plugins;

/// <summary>
/// 插件宿主加载器：扫描插件目录 → 反序列化内嵌 plugin.toml → 过滤（<see cref="StartAsync"/>
/// 传入的禁用名单）→ 依赖校验/拓扑排序 → 实例化/校验 main → 注入/加载/依序启动；
/// Load 或 Start 任一失败 → 逆序 Stop 已 Started 插件（best-effort）→ 原样上抛。
/// 生命周期与主程序同步，无内置状态机。
/// </summary>
public sealed partial class PluginLoader : IDisposable
{
    private readonly PluginService _pluginService;
    private readonly ILogger<PluginLoader> _logger;
    private readonly object _gate = new();
    private readonly List<PluginInfo> _pluginInfos = new();
    private readonly List<CorePlugin> _plugins = new();

    /// <summary>创建插件加载器（注入宿主级 <see cref="PluginService"/>）。</summary>
    public PluginLoader(PluginService pluginService)
    {
        _pluginService = pluginService ?? throw new ArgumentNullException(nameof(pluginService));
        _logger = pluginService.LoggerFactory.CreateLogger<PluginLoader>();
    }

    /// <summary>已发现插件的静态信息（含生命周期状态），按发现顺序。</summary>
    public IReadOnlyList<PluginInfo> Plugins
    {
        get
        {
            lock (_gate)
            {
                return _pluginInfos.ToList();
            }
        }
    }

    /// <summary>扫描、排序并启动全部启用插件。禁用名单由宿主（Core 配置）经
    /// <paramref name="disabledNames"/> 传入。</summary>
    public async Task StartAsync(
        IReadOnlySet<string>? disabledNames = null,
        CancellationToken cancellationToken = default)
    {
        _pluginService.PluginsDirectory.Create();

        AppDomain.CurrentDomain.AssemblyResolve -= OnAssemblyResolve;
        AppDomain.CurrentDomain.AssemblyResolve += OnAssemblyResolve;

        IReadOnlySet<string> disabled = disabledNames ?? new HashSet<string>(StringComparer.Ordinal);
        List<DiscoveredPlugin> discovered = DiscoverManifests();
        List<PluginInfo> ordered = PluginDependencyGraph.Order(
            discovered.Select(d => d.Info), disabled);
        var discoveredByInfo = discovered.ToDictionary(d => d.Info);

        lock (_gate)
        {
            _pluginInfos.AddRange(discovered.Select(d => d.Info));
        }

        foreach (var info in ordered)
        {
            string deps = info.Dependency.Count == 0 ? "none" : string.Join(", ", info.Dependency);
            LogPluginGraphEntry(info.Name, info.Version, deps);
        }

        var startedPlugins = new List<CorePlugin>();
        PluginInfo? failingPlugin = null;
        try
        {
            foreach (var pluginInfo in ordered)
            {
                failingPlugin = pluginInfo;
                cancellationToken.ThrowIfCancellationRequested();

                CorePlugin instance = CreateAndLoad(discoveredByInfo[pluginInfo]);
                lock (_gate)
                {
                    _plugins.Add(instance);
                }

                await instance.StartAsync(cancellationToken).ConfigureAwait(false);
                pluginInfo.State = CorePlugin.PluginState.Started;
                startedPlugins.Add(instance);
                LogPluginStarted(pluginInfo.Name);
            }
        }
        catch (Exception)
        {
            if (failingPlugin is not null
                && failingPlugin.State is not (CorePlugin.PluginState.Started or CorePlugin.PluginState.Stopped))
            {
                failingPlugin.State = CorePlugin.PluginState.Failed;
            }

            await RollbackAsync(startedPlugins).ConfigureAwait(false);
            throw;
        }
    }

    /// <summary>按加载逆序停止全部已启动插件（best-effort，异常聚合记录，不抛出）。</summary>
    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        AppDomain.CurrentDomain.AssemblyResolve -= OnAssemblyResolve;

        List<CorePlugin> snapshot;
        lock (_gate)
        {
            snapshot = _plugins.ToList();
        }

        foreach (var plugin in ((IEnumerable<CorePlugin>)snapshot).Reverse())
        {
            if (plugin.Info.State != CorePlugin.PluginState.Started)
            {
                continue;
            }

            try
            {
                await plugin.StopAsync(cancellationToken).ConfigureAwait(false);
                plugin.Info.State = CorePlugin.PluginState.Stopped;
                LogPluginStopped(plugin.Info.Name);
            }
            catch (Exception ex)
            {
                plugin.Info.State = CorePlugin.PluginState.Failed;
                LogPluginStopFailed(ex, plugin.Info.Name);
            }
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        AppDomain.CurrentDomain.AssemblyResolve -= OnAssemblyResolve;
    }

    private static async Task RollbackAsync(IReadOnlyList<CorePlugin> startedPlugins)
    {
        foreach (var plugin in ((IEnumerable<CorePlugin>)startedPlugins).Reverse())
        {
            try
            {
                await plugin.StopAsync(CancellationToken.None).ConfigureAwait(false);
                plugin.Info.State = CorePlugin.PluginState.Stopped;
            }
            catch
            {
                plugin.Info.State = CorePlugin.PluginState.Failed;
            }
        }
    }

    private List<DiscoveredPlugin> DiscoverManifests()
    {
        var discovered = new List<DiscoveredPlugin>();

        foreach (var dllFile in _pluginService.PluginsDirectory.EnumerateFiles("*.dll", SearchOption.AllDirectories))
        {
            Assembly assembly;
            try
            {
                assembly = Assembly.LoadFrom(dllFile.FullName);
            }
            catch (Exception ex)
            {
                throw new InvalidPluginException($"Failed to load assembly '{dllFile.FullName}'.", ex);
            }

            PluginInfo? info = TryReadManifest(assembly);
            if (info is null)
            {
                continue; // 无 plugin.toml 的 DLL 视为依赖库
            }

                        discovered.Add(new DiscoveredPlugin(info, assembly));
        }

        return discovered;
    }

    private static PluginInfo? TryReadManifest(Assembly assembly)
    {
        string? resourceName = assembly.GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith(".plugin.toml", StringComparison.OrdinalIgnoreCase));
        if (resourceName is null)
        {
            return null;
        }

        using var stream = assembly.GetManifestResourceStream(resourceName);
        if (stream is null)
        {
            throw new InvalidInfoException($"Plugin manifest '{resourceName}' is missing or unreadable.");
        }

        using var reader = new StreamReader(stream);
        return PluginInfo.Parse(reader.ReadToEnd(), resourceName);
    }

    private CorePlugin CreateAndLoad(DiscoveredPlugin discovered)
    {
        PluginInfo info = discovered.Info;
        Type type = ResolveMainType(info, discovered.Assembly);

        CorePlugin instance;
        try
        {
            instance = (CorePlugin)Activator.CreateInstance(type)!;
        }
        catch (Exception ex)
        {
            throw new InvalidPluginException(
                $"Plugin '{info.Name}' main type '{type.FullName}' could not be instantiated.", ex);
        }

        instance.InjectHost(info, _pluginService);
        instance.Load();
        return instance;
    }

    private static Type ResolveMainType(PluginInfo info, Assembly assembly)
    {
        string mainTypeName = info.Main;
        int comma = mainTypeName.IndexOf(',');
        if (comma >= 0)
        {
            mainTypeName = mainTypeName[..comma].Trim();
        }

        if (string.IsNullOrWhiteSpace(mainTypeName))
        {
            throw new InvalidPluginException($"Plugin '{info.Name}' manifest 'main' is not a valid type name.");
        }

        Type? type;
        try
        {
            type = assembly.GetType(mainTypeName);
        }
        catch (Exception ex)
        {
            throw new InvalidPluginException(
                $"Plugin '{info.Name}' main type '{mainTypeName}' could not be resolved.", ex);
        }

        if (type is null)
        {
            throw new InvalidPluginException(
                $"Plugin '{info.Name}' main type '{mainTypeName}' was not found in assembly '{assembly.GetName().Name}'.");
        }

        if (type.IsAbstract || type.IsInterface || !typeof(CorePlugin).IsAssignableFrom(type))
        {
            throw new InvalidPluginException(
                $"Plugin '{info.Name}' main type '{mainTypeName}' must be a concrete {nameof(CorePlugin)} subclass.");
        }

        return type;
    }

    private Assembly? OnAssemblyResolve(object? sender, ResolveEventArgs args)
    {
        string? assemblyName = new AssemblyName(args.Name).Name;
        if (string.IsNullOrEmpty(assemblyName))
        {
            return null;
        }

        FileInfo? candidate = _pluginService.PluginsDirectory
            .EnumerateFiles(assemblyName + ".dll", SearchOption.AllDirectories)
            .FirstOrDefault();
        if (candidate is null)
        {
            return null;
        }

        try
        {
            return Assembly.LoadFrom(candidate.FullName);
        }
        catch (Exception ex)
        {
            LogDependencyLoadFailed(ex, assemblyName, candidate.FullName);
            return null;
        }
    }

    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Information,
        Message = "Plugin graph: {Name} v{Version} (deps: {Deps})")]
    private partial void LogPluginGraphEntry(string name, string version, string deps);

    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Information,
        Message = "Plugin '{Name}' started.")]
    private partial void LogPluginStarted(string name);

    [LoggerMessage(
        EventId = 3,
        Level = LogLevel.Information,
        Message = "Plugin '{Name}' stopped.")]
    private partial void LogPluginStopped(string name);

    [LoggerMessage(
        EventId = 4,
        Level = LogLevel.Error,
        Message = "Failed to stop plugin '{Name}'.")]
    private partial void LogPluginStopFailed(Exception exception, string name);

    [LoggerMessage(
        EventId = 5,
        Level = LogLevel.Error,
        Message = "Failed to load dependency '{AssemblyName}' from '{Path}'.")]
    private partial void LogDependencyLoadFailed(Exception exception, string assemblyName, string path);

    private sealed class DiscoveredPlugin
    {
        public DiscoveredPlugin(PluginInfo info, Assembly assembly)
        {
            Info = info;
            Assembly = assembly;
        }

        public PluginInfo Info { get; }

        public Assembly Assembly { get; }
    }
}
