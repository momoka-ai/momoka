using System.Reflection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Momoka.Core.Events;
using Momoka.Core.Registry;
using Tomlyn;
using Tomlyn.Model;

namespace Momoka.Core.Plugins;

/// <summary>
/// 插件宿主加载器：扫描插件目录 → 解析内嵌 manifest → 过滤（config/plugins.toml 的
/// enabled）→ 依赖校验/拓扑排序 → 实例化/校验 entry → 注入/加载/依序启动；
/// Load 或 Start 任一失败 → 逆序 Stop 已 Started 插件（best-effort）→ 抛
/// <see cref="PluginLoadException"/>。单次运行：重复 Start/Stop 抛
/// <see cref="InvalidOperationException"/>。
/// </summary>
public sealed partial class PluginLoader : IDisposable
{
    private readonly PluginLoaderOptions _options;
    private readonly IServiceRegistry _registry;
    private readonly IEventBus _eventBus;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<PluginLoader> _logger;
    private readonly object _gate = new();
    private readonly List<PluginInfo> _pluginInfos = new();
    private readonly List<LoadedPlugin> _loadedPlugins = new();
    private readonly List<DirectoryInfo> _probeDirectories = new();
    private bool _started;
    private bool _stopped;
    private bool _disposed;

    /// <summary>创建插件加载器。日志工厂可缺省（测试场景使用 Null 日志器）。</summary>
    public PluginLoader(
        PluginLoaderOptions options,
        IServiceRegistry registry,
        IEventBus eventBus,
        ILoggerFactory? loggerFactory = null)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
        _loggerFactory = loggerFactory ?? NullLoggerFactory.Instance;
        _logger = _loggerFactory.CreateLogger<PluginLoader>();
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
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            if (_started)
            {
                throw new InvalidOperationException("Plugin loader has already been started.");
            }

            if (_stopped)
            {
                throw new InvalidOperationException("Plugin loader has already been stopped.");
            }

            _started = true;
        }

        AppDomain.CurrentDomain.AssemblyResolve += OnAssemblyResolve;
        try
        {
            _options.PluginDirectory.Create();
            _options.ConfigDirectory.Create();
            _options.DataDirectory.Create();

            var disabled = ReadDisabledPluginNames();
            List<DiscoveredPlugin> discovered = DiscoverManifests();
            List<PluginInfo> ordered = PluginDependencyGraph.Order(
                discovered.Select(d => d.Info), disabled);
            var discoveredByInfo = discovered.ToDictionary(d => d.Info);

            lock (_gate)
            {
                _pluginInfos.AddRange(discovered.Select(d => d.Info));
            }

            LogPluginGraph(ordered);

            var startedPlugins = new List<LoadedPlugin>();
            PluginInfo? failingPlugin = null;
            try
            {
                foreach (var pluginInfo in ordered)
                {
                    failingPlugin = pluginInfo;
                    cancellationToken.ThrowIfCancellationRequested();

                    LoadedPlugin loaded = CreateAndLoad(discoveredByInfo[pluginInfo]);
                    lock (_gate)
                    {
                        _loadedPlugins.Add(loaded);
                    }

                    await loaded.Instance.StartAsync(cancellationToken).ConfigureAwait(false);
                    loaded.Info.State = PluginState.Started;
                    startedPlugins.Add(loaded);
                    LogPluginStarted(loaded.Info.Name);
                }
            }
            catch (Exception ex)
            {
                if (failingPlugin is not null
                    && failingPlugin.State is not (PluginState.Started or PluginState.Stopped))
                {
                    failingPlugin.State = PluginState.Failed;
                }

                await RollbackAsync(startedPlugins).ConfigureAwait(false);
                if (ex is PluginLoadException)
                {
                    throw;
                }

                throw new PluginLoadException(
                    "Plugin host failed to start; started plugins have been rolled back.", ex);
            }
        }
        catch (Exception ex) when (ex is not PluginLoadException)
        {
            throw new PluginLoadException("Failed to start the plugin host.", ex);
        }
    }

    /// <summary>按加载逆序停止全部已启动插件（best-effort，异常聚合记录，不抛出）。</summary>
    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        List<LoadedPlugin> snapshot;
        lock (_gate)
        {
            if (!_started || _stopped)
            {
                throw new InvalidOperationException("Plugin loader is not started or has already been stopped.");
            }

            _stopped = true;
            snapshot = _loadedPlugins.ToList();
        }

        AppDomain.CurrentDomain.AssemblyResolve -= OnAssemblyResolve;

        foreach (var loaded in ((IEnumerable<LoadedPlugin>)snapshot).Reverse())
        {
            if (loaded.Info.State != PluginState.Started)
            {
                continue;
            }

            try
            {
                await loaded.Instance.StopAsync(cancellationToken).ConfigureAwait(false);
                loaded.Info.State = PluginState.Stopped;
                LogPluginStopped(loaded.Info.Name);
            }
            catch (Exception ex)
            {
                loaded.Info.State = PluginState.Failed;
                LogPluginStopFailed(ex, loaded.Info.Name);
            }
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        AppDomain.CurrentDomain.AssemblyResolve -= OnAssemblyResolve;
    }

    private static async Task RollbackAsync(IReadOnlyList<LoadedPlugin> startedPlugins)
    {
        foreach (var loaded in ((IEnumerable<LoadedPlugin>)startedPlugins).Reverse())
        {
            try
            {
                await loaded.Instance.StopAsync(CancellationToken.None).ConfigureAwait(false);
                loaded.Info.State = PluginState.Stopped;
            }
            catch
            {
                loaded.Info.State = PluginState.Failed;
            }
        }
    }

    private void LogPluginGraph(IReadOnlyList<PluginInfo> ordered)
    {
        foreach (var info in ordered)
        {
            string deps = info.DependsOn.Count == 0 ? "none" : string.Join(", ", info.DependsOn);
            LogPluginGraphEntry(info.Name, info.Version, deps);
        }
    }

    private HashSet<string> ReadDisabledPluginNames()
    {
        var configFile = new FileInfo(Path.Combine(_options.ConfigDirectory.FullName, "plugins.toml"));
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
            throw new PluginLoadException($"Failed to read host plugin config '{configFile.FullName}'.", ex);
        }

        TomlTable table;
        try
        {
            table = TomlSerializer.Deserialize<TomlTable>(
                    text, new TomlSerializerOptions { SourceName = configFile.FullName })
                ?? throw new PluginLoadException($"Failed to parse host plugin config '{configFile.FullName}'.");
        }
        catch (TomlException ex)
        {
            throw new PluginLoadException($"Failed to parse host plugin config '{configFile.FullName}'.", ex);
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
        var probeDirectories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var dllFile in _options.PluginDirectory.EnumerateFiles("*.dll", SearchOption.AllDirectories))
        {
            Assembly assembly;
            try
            {
                assembly = Assembly.LoadFrom(dllFile.FullName);
            }
            catch (Exception ex)
            {
                throw new PluginLoadException($"Failed to load assembly '{dllFile.FullName}'.", ex);
            }

            PluginManifest? manifest = TryReadManifest(assembly);
            if (manifest is null)
            {
                continue;
            }

            if (dllFile.DirectoryName is not null)
            {
                probeDirectories.Add(dllFile.DirectoryName);
            }

            discovered.Add(new DiscoveredPlugin(
                new PluginInfo(
                    manifest.Name,
                    manifest.Version,
                    manifest.Entry,
                    manifest.DependsOn,
                    dllFile.Directory ?? _options.PluginDirectory),
                assembly));
        }

        lock (_gate)
        {
            _probeDirectories.Clear();
            _probeDirectories.AddRange(probeDirectories.Select(d => new DirectoryInfo(d)));
        }

        return discovered;
    }

    private static PluginManifest? TryReadManifest(Assembly assembly)
    {
        string? resourceName = assembly.GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith(".plugin.toml", StringComparison.OrdinalIgnoreCase));
        if (resourceName is null)
        {
            return null;
        }

        string toml;
        using (var stream = assembly.GetManifestResourceStream(resourceName))
        {
            if (stream is null)
            {
                return null;
            }

            using var reader = new StreamReader(stream);
            toml = reader.ReadToEnd();
        }

        return PluginManifest.Parse(toml, resourceName);
    }

    private LoadedPlugin CreateAndLoad(DiscoveredPlugin discovered)
    {
        PluginInfo info = discovered.Info;
        Type type = ResolveEntryType(discovered);

        CorePlugin instance;
        try
        {
            instance = (CorePlugin)Activator.CreateInstance(type)!;
        }
        catch (Exception ex)
        {
            throw new PluginLoadException(
                $"Plugin '{info.Name}' entry type '{type.FullName}' could not be instantiated.", ex);
        }

        instance.Name = info.Name;
        instance.Version = info.Version;
        if (!string.Equals(instance.Name, info.Name, StringComparison.Ordinal)
            || !string.Equals(instance.Version, info.Version, StringComparison.Ordinal))
        {
            throw new PluginLoadException($"Plugin '{info.Name}' name/version does not match its manifest.");
        }

        var pluginService = new PluginService(
            info.Name,
            _registry,
            _eventBus,
            _loggerFactory.CreateLogger(info.Name),
            new DirectoryInfo(Path.Combine(_options.DataDirectory.FullName, "Plugins")),
            new DirectoryInfo(Path.Combine(_options.ConfigDirectory.FullName, "Plugins")));
        instance.InjectHost(pluginService);

        try
        {
            instance.Load();
        }
        catch (Exception ex)
        {
            throw new PluginLoadException($"Plugin '{info.Name}' failed to load.", ex);
        }

        info.State = PluginState.Loaded;
        return new LoadedPlugin(info, instance);
    }

    private static Type ResolveEntryType(DiscoveredPlugin discovered)
    {
        PluginInfo info = discovered.Info;
        string entryTypeName = info.Entry;
        int comma = entryTypeName.IndexOf(',');
        if (comma >= 0)
        {
            entryTypeName = entryTypeName[..comma].Trim();
        }

        if (string.IsNullOrWhiteSpace(entryTypeName))
        {
            throw new PluginLoadException($"Plugin '{info.Name}' manifest 'entry' is not a valid type name.");
        }

        Type? type;
        try
        {
            type = discovered.Assembly.GetType(entryTypeName);
        }
        catch (Exception ex)
        {
            throw new PluginLoadException(
                $"Plugin '{info.Name}' entry type '{entryTypeName}' could not be resolved.", ex);
        }

        if (type is null)
        {
            throw new PluginLoadException(
                $"Plugin '{info.Name}' entry type '{entryTypeName}' was not found in assembly '{discovered.Assembly.GetName().Name}'.");
        }

        if (type.IsAbstract || type.IsInterface || !typeof(CorePlugin).IsAssignableFrom(type))
        {
            throw new PluginLoadException(
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

        List<DirectoryInfo> probeDirectories;
        lock (_gate)
        {
            probeDirectories = _probeDirectories.ToList();
        }

        foreach (var directory in probeDirectories)
        {
            string candidate = Path.Combine(directory.FullName, assemblyName + ".dll");
            if (!File.Exists(candidate))
            {
                continue;
            }

            try
            {
                return Assembly.LoadFrom(candidate);
            }
            catch (Exception ex)
            {
                LogDependencyLoadFailed(ex, assemblyName, candidate);
            }
        }

        return null;
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

    private sealed class LoadedPlugin
    {
        public LoadedPlugin(PluginInfo info, CorePlugin instance)
        {
            Info = info;
            Instance = instance;
        }

        public PluginInfo Info { get; }

        public CorePlugin Instance { get; }
    }

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
