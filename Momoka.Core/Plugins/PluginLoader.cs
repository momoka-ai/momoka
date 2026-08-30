using System.Reflection;
using System.Runtime.Loader;
using Momoka.Core.Behaviors;

namespace Momoka.Core.Plugins;

/// <summary>
/// 插件宿主加载器：Load 逐插件实例化并记录（<see cref="PluginAssembly"/> + <see cref="Plugin"/>），
/// EnableAsync / DisableAsync 驱动生命周期（OnEnable / OnDisable），批量启停按依赖图拓扑顺序执行；
/// 静态内省原语提供文件级扫描 / manifest / 资源 / 主类解析。生命周期与主程序同步，无内置状态机。
/// </summary>
public sealed class PluginLoader : IDisposable
{
    private readonly PluginService _pluginService;
    private readonly object _gate = new();
    private readonly List<PluginAssembly> _assemblies = new();
    private readonly List<Plugin> _plugins = new();

    /// <summary>创建插件加载器（注入宿主级 <see cref="PluginService"/>）。</summary>
    public PluginLoader(PluginService pluginService)
    {
        _pluginService = pluginService ?? throw new ArgumentNullException(nameof(pluginService));
        AppDomain.CurrentDomain.AssemblyResolve += OnAssemblyResolve;
    }

    /// <summary>已加载插件文件记录（快照，按加载顺序）。</summary>
    public IReadOnlyList<PluginAssembly> PluginAssemblies
    {
        get
        {
            lock (_gate)
            {
                return _assemblies.ToList();
            }
        }
    }

    /// <summary>已加载插件实例（快照，按加载顺序）。</summary>
    public IReadOnlyList<Plugin> Plugins
    {
        get
        {
            lock (_gate)
            {
                return _plugins.ToList();
            }
        }
    }

    /// <summary>
    /// 从程序集文件加载插件：解析 manifest → 校验并解析主类 → 实例化 → 注入宿主能力 →
    /// 记录进 <see cref="PluginAssemblies"/> 与 <see cref="Plugins"/>（状态 Loaded），不调用 OnEnable。
    /// 非插件程序集 / 主类非法 / 重复名 → 抛 <see cref="InvalidPluginException"/>。
    /// </summary>
    public Plugin Load(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        Assembly assembly;
        try
        {
            assembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(path);
        }
        catch (Exception ex) when (ex is IOException or BadImageFormatException or FileLoadException)
        {
            throw new InvalidPluginException($"Failed to load assembly '{path}'.", ex);
        }

        PluginInfo? info = GetPluginInfo(path);
        if (info is null)
        {
            throw new InvalidPluginException($"Assembly '{path}' is not a plugin (missing plugin.toml).");
        }

        lock (_gate)
        {
            if (_plugins.Any(p => p.Name == info.Name))
            {
                throw new InvalidPluginException($"Duplicate plugin name '{info.Name}'.");
            }
        }

        // 扫描 Behavior 派生类型注册进 Gateway（四件套契约校验，缺 Execute 等 fail-fast）
        ScanEventRouters(assembly);

        Type? mainType = GetPluginMainType(info, assembly);
        if (mainType is null)
        {
            throw new InvalidPluginException(
                $"Plugin '{info.Name}' main type '{info.Main}' was not found or is not a concrete {nameof(Plugin)} subclass.");
        }

        Plugin plugin;
        try
        {
            plugin = (Plugin)Activator.CreateInstance(mainType)!;
        }
        catch (Exception ex) when (ex is TargetInvocationException or MemberAccessException
            or TypeLoadException or TypeInitializationException or NotSupportedException)
        {
            throw new InvalidPluginException(
                $"Plugin '{info.Name}' main type '{mainType.FullName}' could not be instantiated.", ex);
        }

        plugin.InjectHost(info, _pluginService);

        lock (_gate)
        {
            _assemblies.Add(new PluginAssembly(path, info, assembly));
            _plugins.Add(plugin);
        }

        return plugin;
    }

    /// <summary>
    /// 启用单个插件（须已加载且状态为 Loaded 或 Disabled）：调用 OnEnable。
    /// 插件未加载 / 不在可启用状态（如已启用或 Failed）→ false；OnEnable 抛异常 → 置 Failed 并返回 false。
    /// </summary>
    public bool EnableAsync(Plugin plugin)
    {
        ArgumentNullException.ThrowIfNull(plugin);
        lock (_gate)
        {
            if (!_plugins.Contains(plugin))
            {
                return false;
            }
        }

        if (plugin.State is not (PluginState.Loaded or PluginState.Disabled))
        {
            return false;
        }

        try
        {
            plugin.OnEnable();
        }
        catch (Exception)
        {
            plugin.State = PluginState.Failed;
            return false;
        }

        plugin.State = PluginState.Enabled;
        return true;
    }

    /// <summary>
    /// 停用单个插件（须已启用）：调用 OnDisable（清理由插件自行完成）。
    /// 插件未加载 / 未启用 → false；OnDisable 抛异常 → 置 Failed 并返回 false。
    /// </summary>
    public bool DisableAsync(Plugin plugin)
    {
        ArgumentNullException.ThrowIfNull(plugin);
        lock (_gate)
        {
            if (!_plugins.Contains(plugin))
            {
                return false;
            }
        }

        if (plugin.State != PluginState.Enabled)
        {
            return false;
        }

        try
        {
            plugin.OnDisable();
        }
        catch (Exception)
        {
            plugin.State = PluginState.Failed;
            return false;
        }

        plugin.State = PluginState.Disabled;
        return true;
    }

    /// <summary>
    /// 按依赖图拓扑顺序启用全部已加载插件。任一启用失败 → 逆序回滚已启用插件并返回 false；
    /// 依赖图结构性错误（重复名 / 环）→ fail-fast 抛 <see cref="InvalidPluginException"/>。
    /// </summary>
    public Task<bool> EnableAsync()
    {
        var ordered = GetPluginsInDependencyOrder();
        var enabled = new List<Plugin>();
        foreach (var plugin in ordered)
        {
            if (!EnableAsync(plugin))
            {
                for (int i = enabled.Count - 1; i >= 0; i--)
                {
                    DisableAsync(enabled[i]);
                }

                return Task.FromResult(false);
            }

            enabled.Add(plugin);
        }

        return Task.FromResult(true);
    }

    /// <summary>
    /// 按依赖图逆拓扑顺序停用全部插件。任一停用失败 → 返回 false（已停用的保持停用）。
    /// 依赖图结构性错误（重复名 / 环）→ fail-fast 抛 <see cref="InvalidPluginException"/>。
    /// </summary>
    public Task<bool> DisableAsync()
    {
        var ordered = GetPluginsInDependencyOrder();
        for (int i = ordered.Count - 1; i >= 0; i--)
        {
            if (!DisableAsync(ordered[i]))
            {
                return Task.FromResult(false);
            }
        }

        return Task.FromResult(true);
    }

    /// <summary>按名称查找已加载插件；未找到返回 null。</summary>
    public Plugin? GetPlugin(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        lock (_gate)
        {
            return _plugins.FirstOrDefault(p => p.Name == name);
        }
    }

    /// <summary>该插件依赖的已加载插件（硬前置 + 可解析的软前置）。</summary>
    public IReadOnlyCollection<Plugin> GetPluginDependencies(Plugin plugin)
    {
        ArgumentNullException.ThrowIfNull(plugin);
        lock (_gate)
        {
            Plugin? source = _plugins.FirstOrDefault(p => ReferenceEquals(p, plugin) || p.Name == plugin.Name);
            if (source is null)
            {
                return Array.Empty<Plugin>();
            }

            var byName = _plugins.ToDictionary(p => p.Name, StringComparer.Ordinal);
            return source.Info.Dependency
                .Concat(source.Info.DependencyOptional)
                .Where(byName.ContainsKey)
                .Select(n => byName[n])
                .ToList();
        }
    }

    /// <summary>依赖该插件的全部已加载插件（反向依赖）。</summary>
    public IReadOnlyCollection<Plugin> GetPluginDependents(Plugin plugin)
    {
        ArgumentNullException.ThrowIfNull(plugin);
        lock (_gate)
        {
            return _plugins
                .Where(p => p.Info.Dependency.Contains(plugin.Name)
                    || p.Info.DependencyOptional.Contains(plugin.Name))
                .ToList();
        }
    }

    /// <summary>递归枚举目录内所有候选插件文件（<c>*.dll</c>，仅路径，不做内部解析）。</summary>
    public static IEnumerable<string> GetPluginFiles(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        return Directory.EnumerateFiles(path, "*.dll", SearchOption.AllDirectories);
    }

    /// <summary>从程序集文件按内部资源路径读取嵌入资源，返回可读流；资源不存在抛 <see cref="InvalidPluginException"/>。</summary>
    public static Stream GetPluginResource(string path, string inner)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentException.ThrowIfNullOrWhiteSpace(inner);

        Assembly assembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(path);
        Stream? stream = assembly.GetManifestResourceStream(inner);
        return stream ?? throw new InvalidPluginException($"Resource '{inner}' was not found in assembly '{path}'.");
    }

    /// <summary>尝试从程序集文件读取插件描述（内嵌 plugin.toml）；非插件程序集或读取失败返回 null。</summary>
    public static PluginInfo? GetPluginInfo(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        Assembly assembly;
        try
        {
            assembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(path);
        }
        catch (Exception ex) when (ex is IOException or BadImageFormatException or FileLoadException)
        {
            return null;
        }

        string? resourceName = assembly.GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith(".plugin.toml", StringComparison.OrdinalIgnoreCase));
        if (resourceName is null)
        {
            return null;
        }

        using Stream stream = GetPluginResource(path, resourceName);
        using var reader = new StreamReader(stream);
        return PluginInfo.Parse(reader.ReadToEnd(), resourceName);
    }

    /// <summary>从程序集中解析插件主类（info.Main，须为具体 <see cref="Plugin"/> 子类）；非法返回 null。</summary>
    public static Type? GetPluginMainType(PluginInfo info, Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(info);
        ArgumentNullException.ThrowIfNull(assembly);

        string mainTypeName = info.Main;
        int comma = mainTypeName.IndexOf(',');
        if (comma >= 0)
        {
            mainTypeName = mainTypeName[..comma].Trim();
        }

        if (string.IsNullOrWhiteSpace(mainTypeName))
        {
            return null;
        }

        Type? type;
        try
        {
            type = assembly.GetType(mainTypeName);
        }
        catch (Exception ex) when (ex is ArgumentException or TypeLoadException
            or IOException or BadImageFormatException)
        {
            return null;
        }

        if (type is null || type.IsAbstract || type.IsInterface || !typeof(Plugin).IsAssignableFrom(type))
        {
            return null;
        }

        return type;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        AppDomain.CurrentDomain.AssemblyResolve -= OnAssemblyResolve;
    }

    /// <summary>快照全部已加载插件并按依赖图拓扑排序（结构性错误 fail-fast）。</summary>
    private List<Plugin> GetPluginsInDependencyOrder()
    {
        List<Plugin> snapshot;
        lock (_gate)
        {
            snapshot = _plugins.ToList();
        }

        var byInfo = snapshot.ToDictionary(p => p.Info);
        return PluginDependencyGraph.Order(snapshot.Select(p => p.Info))
            .Select(i => byInfo[i])
            .ToList();
    }

    /// <summary>
    /// 扫描程序集（Load 期）：<see cref="Behavior"/> 派生类型注册进 Gateway
    /// （四件套契约校验，缺 Execute 等 fail-fast）。OnEnable 无需显式注册任何事件；
    /// 可传输契约（[Publish]）由发布路径按属性判定，无需注册表。
    /// </summary>
    private void ScanEventRouters(Assembly assembly)
    {
        foreach (Type type in assembly.GetTypes())
        {
            if (IsBehavior(type))
            {
                _pluginService.Gateway.RegisterBehavior(type, _pluginService);
            }
        }
    }

    /// <summary>该类型是否为 <see cref="Behavior"/> 派生的具体类型。</summary>
    private static bool IsBehavior(Type type) =>
        !type.IsAbstract && !type.IsInterface && typeof(Behavior).IsAssignableFrom(type);

    private Assembly? OnAssemblyResolve(object? sender, ResolveEventArgs args)
    {        string? assemblyName = new AssemblyName(args.Name).Name;
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
            return AssemblyLoadContext.Default.LoadFromAssemblyPath(candidate.FullName);
        }
        catch (Exception ex) when (ex is IOException or BadImageFormatException or FileLoadException)
        {
            return null;
        }
    }

    /// <summary>已加载插件文件的记录（文件级元数据，与 <see cref="Plugin"/> 实例一一对应）。</summary>
    public sealed record PluginAssembly(string Path, PluginInfo Info, Assembly Assembly);
}
