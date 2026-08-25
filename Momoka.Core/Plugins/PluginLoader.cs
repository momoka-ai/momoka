using System.Reflection;
using Microsoft.Extensions.Logging;
using Tomlyn;
using Tomlyn.Model;

namespace Momoka.Core.Plugins;

/// <summary>
/// 插件宿主加载器：扫描插件目录 → 反序列化内嵌 plugin.toml → 过滤（Config/plugins.toml 的
/// enabled）→ 依赖校验/拓扑排序 → 实例化/校验 entry → 注入/加载/依序启动；
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

    /// <summary>扫描、排序并启动全部启用插件。</summary>
    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        _pluginService.PluginsDirectory.Create();
        _pluginService.ConfigDirectory.Create();
        _pluginService.DataDirectory.Create();

        AppDomain.CurrentDomain.AssemblyResolve -= OnAssemblyResolve;
        AppDomain.CurrentDomain.AssemblyResolve += OnAssemblyResolve;

        IReadOnlySet<string> disabled = ReadDisabledPluginNames();
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
            string deps = info.DependsOn.Count == 0 ? "none" : string.Join(", ", info.DependsOn);
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
                pluginInfo.State = PluginState.Started;
                startedPlugins.Add(instance);
                LogPluginStarted(pluginInfo.Name);
            }
        }
        catch (Exception)
        {
            if (failingPlugin is not null
                && failingPlugin.State is not (PluginState.Started or PluginState.Stopped))
            {
                failingPlugin.State = PluginState.Failed;
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
            if (plugin.Info.State != PluginState.Started)
            {
                continue;
            }

            try
            {
                await plugin.StopAsync(cancellationToken).ConfigureAwait(false);
                plugin.Info.State = PluginState.Stopped;
                LogPluginStopped(plugin.Info.Name);
            }
            catch (Exception ex)
            {
                plugin.Info.State = PluginState.Failed;
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
                plugin.Info.State = PluginState.Stopped;
            }
            catch
            {
                plugin.Info.State = PluginState.Failed;
            }
        }
    }

    private HashSet<string> ReadDisabledPluginNames()
    {
        var configFile = new FileInfo(Path.Combine(_pluginService.ConfigDirectory.FullName, "plugins.toml"));
        if (!configFile.Exists)
        {
            return new HashSet<string>(StringComparer.Ordinal);
        }

        string text;
        try
        {
            text = File.ReadAllText(configFile.FullName);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to read host plugin config '{configFile.FullName}'.", ex);
        }

        TomlTable table;
        try
        {
            table = TomlSerializer.Deserialize<TomlTable>(
                    text, new TomlSerializerOptions { SourceName = configFile.FullName })
                ?? throw new InvalidOperationException($"Failed to parse host plugin config '{configFile.FullName}'.");
        }
        catch (TomlException ex)
        {
            throw new InvalidOperationException($"Failed to parse host plugin config '{configFile.FullName}'.", ex);
        }

        var disabled = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (name, value) in table)
        {
            if (value is TomlTable pluginSection
                && pluginSection.TryGetValue("enabled", out var enabled)
                && enabled is bool enabledFlag
                && !enabledFlag)
            {
                disabled.Add(name);
            }
        }

        return disabled;
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

            info.Location = dllFile.Directory ?? _pluginService.PluginsDirectory;
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
        Type type = ResolveEntryType(info, discovered.Assembly);

        CorePlugin instance;
        try
        {
            instance = (CorePlugin)Activator.CreateInstance(type)!;
        }
        catch (Exception ex)
        {
            throw new InvalidPluginException(
                $"Plugin '{info.Name}' entry type '{type.FullName}' could not be instantiated.", ex);
        }

        instance.InjectHost(info, _pluginService.ForPlugin(info.Name));
        instance.Load();
        info.State = PluginState.Loaded;
        return instance;
    }

    private static Type ResolveEntryType(PluginInfo info, Assembly assembly)
    {
        string entryTypeName = info.Entry;
        int comma = entryTypeName.IndexOf(',');
        if (comma >= 0)
        {
            entryTypeName = entryTypeName[..comma].Trim();
        }

        if (string.IsNullOrWhiteSpace(entryTypeName))
        {
            throw new InvalidPluginException($"Plugin '{info.Name}' manifest 'entry' is not a valid type name.");
        }

        Type? type;
        try
        {
            type = assembly.GetType(entryTypeName);
        }
        catch (Exception ex)
        {
            throw new InvalidPluginException(
                $"Plugin '{info.Name}' entry type '{entryTypeName}' could not be resolved.", ex);
        }

        if (type is null)
        {
            throw new InvalidPluginException(
                $"Plugin '{info.Name}' entry type '{entryTypeName}' was not found in assembly '{assembly.GetName().Name}'.");
        }

        if (type.IsAbstract || type.IsInterface || !typeof(CorePlugin).IsAssignableFrom(type))
        {
            throw new InvalidPluginException(
                $"Plugin '{info.Name}' entry type '{entryTypeName}' must be a concrete {nameof(CorePlugin)} subclass.");
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
