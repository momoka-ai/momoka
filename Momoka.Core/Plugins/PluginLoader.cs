using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.Loader;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Momoka.Core.Events;
using Momoka.Core.Services;

namespace Momoka.Core.Plugins;

/// <summary>
/// 插件宿主加载器（声明式生命周期）：Load 扫描单文件（plugin.toml → 主类静态
/// <c>Build(Plugin)</c> → 声明填充）；Enable 应用声明（服务注册 → [ServiceInjection] 注入 →
/// 事件监听器注册）；Disable 逆序回收（先守卫服务消费者 → 反注册监听器 → 按声明类型移除服务）。
/// 批量启停按 manifest 依赖图拓扑序执行；运行期单插件启停受 <see cref="ServiceUsageGraph"/> 守卫
/// （提供商仍有已启用消费者时禁用 → fail-fast）。生命周期与主程序同步，无内置状态机。
/// </summary>
public sealed class PluginLoader
{
    private static readonly ConcurrentDictionary<Type, MethodInfo> TryRegisterMethods = new();
    private static readonly ConcurrentDictionary<Type, MethodInfo> RemoveMethods = new();

    private readonly string _pluginsDirectory;
    private readonly EventHub _events;
    private readonly ILoggerFactory? _loggerFactory;
    private readonly object _gate = new();
    private readonly List<Plugin> _plugins = new();
    private readonly Dictionary<Plugin, PluginState> _states = new();
    private readonly ServiceUsageGraph _graph = new();

    /// <summary>创建插件加载器。插件根目录（Plugins）与事件中心由宿主注入。</summary>
    public PluginLoader(string pluginsDirectory, EventHub events, ILoggerFactory? loggerFactory = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginsDirectory);
        ArgumentNullException.ThrowIfNull(events);
        _pluginsDirectory = Path.GetFullPath(pluginsDirectory);
        _events = events;
        _loggerFactory = loggerFactory ?? NullLoggerFactory.Instance;
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

    /// <summary>插件当前生命周期状态（未知插件返回 Loaded）。</summary>
    public PluginState GetState(Plugin plugin)
    {
        ArgumentNullException.ThrowIfNull(plugin);
        lock (_gate)
        {
            return _states.TryGetValue(plugin, out PluginState state) ? state : PluginState.Loaded;
        }
    }

    /// <summary>
    /// 从程序集文件加载插件：解析内嵌 plugin.toml → 主类（info.Main）静态 Build(Plugin) 签名校验 →
    /// 构造 Plugin 声明面并执行 Build。非插件程序集 / 主类或 Build 缺失 / 重复名 → 抛
    /// <see cref="InvalidPluginException"/>。Build 抛异常 → 同样 fail-fast（解包 TargetInvocationException）。
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

        PluginInfo info = ReadManifest(assembly)
            ?? throw new InvalidPluginException($"Assembly '{path}' is not a plugin (missing plugin.toml).");

        Type mainType = ResolveMainType(info, assembly);
        MethodInfo build = FindBuild(mainType, path);

        try
        {
            return RegisterPlugin(info, plugin => build.Invoke(null, new object[] { plugin }));
        }
        catch (TargetInvocationException ex)
        {
            throw new InvalidPluginException(
                $"Plugin '{info.Name}' Build failed: {ex.InnerException?.Message}", ex.InnerException ?? ex);
        }
    }

    /// <summary>
    /// 进程内注册插件（宿主内嵌/测试用）：以 <paramref name="info"/> 构造 Plugin 声明面并执行
    /// <paramref name="build"/>，记录进加载器（状态 Loaded）。重复名 → fail-fast。
    /// <see cref="Load(string)"/> 亦经由本入口落地（build = 主类静态 Build）。
    /// </summary>
    internal Plugin RegisterPlugin(PluginInfo info, Action<Plugin> build)
    {
        ArgumentNullException.ThrowIfNull(info);
        ArgumentNullException.ThrowIfNull(build);

        lock (_gate)
        {
            if (_plugins.Any(p => p.Name == info.Name))
            {
                throw new InvalidPluginException($"Duplicate plugin name '{info.Name}'.");
            }
        }

        var plugin = new Plugin(info, _pluginsDirectory, _loggerFactory);
        build(plugin);

        lock (_gate)
        {
            _plugins.Add(plugin);
            _states[plugin] = PluginState.Loaded;
        }

        return plugin;
    }

    /// <summary>启用单个插件（须已加载且状态 Loaded/Disabled）：确保服务已注册 → [ServiceInjection] 注入并记录
    /// 使用边 → 注册事件监听器 → Enabled。注入/注册抛异常 → 回滚已生效部分并置 Failed，返回 false。
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

        if (GetState(plugin) is not (PluginState.Loaded or PluginState.Disabled))
        {
            return false;
        }

        try
        {
            EnsureServicesRegistered(plugin);
            ServiceInjector.Inject(plugin, _graph);
            foreach (object listener in plugin.EventHandlers)
            {
                _events.Register(listener);
            }
        }
        catch (Exception)
        {
            UndoEnable(plugin);
            SetState(plugin, PluginState.Failed);
            return false;
        }

        SetState(plugin, PluginState.Enabled);
        return true;
    }

    /// <summary>
    /// 停用单个插件（须已启用）：提供商仍有已启用消费者 → fail-fast 抛
    /// <see cref="InvalidOperationException"/>（须先停用消费者）；否则反注册监听器 → 按声明类型
    /// 移除服务 → Disabled。
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

        if (GetState(plugin) != PluginState.Enabled)
        {
            return false;
        }

        IReadOnlyList<Plugin> users = _graph.GetUsers(plugin)
            .Where(user => GetState(user) == PluginState.Enabled)
            .ToList();
        if (users.Count > 0)
        {
            throw new InvalidOperationException(
                $"Plugin '{plugin.Name}' is still used by enabled plugin(s): " +
                $"{string.Join(", ", users.Select(u => u.Name))}. Disable consumers first.");
        }

        try
        {
            foreach (object listener in plugin.EventHandlers)
            {
                _events.Unregister(listener);
            }

            RemoveServices(plugin);
        }
        catch (Exception)
        {
            SetState(plugin, PluginState.Failed);
            return false;
        }

        SetState(plugin, PluginState.Disabled);
        return true;
    }

    /// <summary>按依赖图拓扑序启用全部已加载插件；任一失败 → 逆序回滚并返回 false。</summary>
    public bool EnableAsync()
    {
        var enabled = new List<Plugin>();
        foreach (Plugin plugin in GetPluginsInDependencyOrder())
        {
            if (!EnableAsync(plugin))
            {
                for (int i = enabled.Count - 1; i >= 0; i--)
                {
                    DisableAsync(enabled[i]);
                }

                return false;
            }

            enabled.Add(plugin);
        }

        return true;
    }

    /// <summary>按依赖图逆拓扑序停用全部已加载插件；任一失败返回 false（已停用的保持停用）。</summary>
    public bool DisableAsync()
    {
        List<Plugin> order = GetPluginsInDependencyOrder();
        for (int i = order.Count - 1; i >= 0; i--)
        {
            if (!DisableAsync(order[i]))
            {
                return false;
            }
        }

        return true;
    }

    private static PluginInfo? ReadManifest(Assembly assembly)
    {
        string? resourceName = assembly.GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith(".plugin.toml", StringComparison.OrdinalIgnoreCase));
        if (resourceName is null)
        {
            return null;
        }

        using Stream stream = assembly.GetManifestResourceStream(resourceName)!;
        using var reader = new StreamReader(stream);
        return PluginInfo.Parse(reader.ReadToEnd(), resourceName);
    }

    private static Type ResolveMainType(PluginInfo info, Assembly assembly)
    {
        string typeName = info.Main;
        int comma = typeName.IndexOf(',');
        if (comma >= 0)
        {
            typeName = typeName[..comma].Trim();
        }

        Type? type = assembly.GetType(typeName);
        if (type is null)
        {
            throw new InvalidPluginException(
                $"Plugin '{info.Name}' main type '{info.Main}' was not found.");
        }

        return type;
    }

    private static MethodInfo FindBuild(Type mainType, string path)
    {
        const BindingFlags flags = BindingFlags.Public | BindingFlags.Static;
        MethodInfo? build = mainType.GetMethod("Build", flags, null, new[] { typeof(Plugin) }, null);
        if (build is null || build.ReturnType != typeof(void))
        {
            throw new InvalidPluginException(
                $"Plugin main type '{mainType.FullName}' must declare a static void Build({nameof(Plugin)}).");
        }

        return build;
    }

    private void EnsureServicesRegistered(Plugin plugin)
    {
        foreach (Plugin.ServiceProviderRegistration registration in plugin.ServiceProviders)
        {
            MethodInfo register = TryRegisterMethods.GetOrAdd(
                registration.ServiceType,
                serviceType => typeof(Service<>).MakeGenericType(serviceType)
                    .GetMethod("TryRegister", BindingFlags.Public | BindingFlags.Static)!);
            register.Invoke(null, new object?[] { registration.Provider, plugin });
        }
    }

    private void RemoveServices(Plugin plugin)
    {
        foreach (Plugin.ServiceProviderRegistration registration in plugin.ServiceProviders)
        {
            MethodInfo remove = RemoveMethods.GetOrAdd(
                registration.ServiceType,
                serviceType => typeof(Service<>).MakeGenericType(serviceType)
                    .GetMethod("Remove", BindingFlags.Public | BindingFlags.Static)!);
            remove.Invoke(null, new object[] { plugin });
        }
    }

    private void UndoEnable(Plugin plugin)
    {
        foreach (object listener in plugin.EventHandlers)
        {
            _events.Unregister(listener);
        }

        RemoveServices(plugin);
    }

    private void SetState(Plugin plugin, PluginState state)
    {
        lock (_gate)
        {
            _states[plugin] = state;
        }
    }

    private List<Plugin> GetPluginsInDependencyOrder()
    {
        List<Plugin> snapshot;
        lock (_gate)
        {
            snapshot = _plugins.ToList();
        }

        var byName = snapshot.ToDictionary(p => p.Name, StringComparer.Ordinal);
        return PluginDependencyGraph.Order(snapshot.Select(p => p.Info))
            .Select(i => byName[i.Name])
            .ToList();
    }
}
