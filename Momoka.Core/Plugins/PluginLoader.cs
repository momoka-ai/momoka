using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;

namespace Momoka.Core.Plugins;

/// <summary>
/// 插件宿主加载器（声明式生命周期）：Load 扫描单文件（plugin.toml → 主类静态
/// <c>Build(Plugin)</c> → 声明填充）；Enable = 插件声明的服务描述符与事件监听器随
/// <see cref="PluginService.Add"/> 进入组合；Disable = <see cref="PluginService.Remove"/> 移出组合。
/// 生命周期状态挂在 <see cref="Plugin.State"/> 上，批量启停按 manifest 依赖图拓扑序执行。
/// 假定宿主串行调用（初始化/启停单线程），自身不加锁、无内置状态机。
/// </summary>
public sealed class PluginLoader
{
    private readonly PluginService _services;
    private readonly List<Plugin> _plugins = new();

    /// <summary>创建插件加载器。运行时服务（组合/事件）由宿主注入。</summary>
    public PluginLoader(PluginService services)
    {
        _services = services;
    }

    /// <summary>已加载插件实例（快照，按加载顺序）。</summary>
    public IReadOnlyList<Plugin> Plugins => _plugins.ToList();

    /// <summary>
    /// 从程序集文件加载插件：解析内嵌 plugin.toml → 主类（info.Main）静态 Build(Plugin) 签名校验 →
    /// 构造 Plugin 声明面并执行 Build。非插件程序集 / 主类或 Build 缺失 / 重复名 → 抛
    /// <see cref="InvalidPluginException"/>。Build 抛异常 → 同样 fail-fast（解包 TargetInvocationException）。
    /// </summary>
    public Plugin Load(string path)
    {
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
    /// <paramref name="build"/>，记录进加载器（<see cref="Plugin.State"/> = Loaded）。
    /// 重复名 → fail-fast。<see cref="Load(string)"/> 亦经由本入口落地（build = 主类静态 Build）。
    /// </summary>
    internal Plugin RegisterPlugin(PluginInfo info, Action<Plugin> build)
    {
        if (_plugins.Any(p => p.Name == info.Name))
        {
            throw new InvalidPluginException($"Duplicate plugin name '{info.Name}'.");
        }

        var plugin = new Plugin(info);
        build(plugin);

        _plugins.Add(plugin);
        plugin.State = PluginState.Loaded;

        return plugin;
    }

    /// <summary>启用单个插件（须已加载且状态 Loaded/Disabled）：进入组合并置 Enabled。</summary>
    public bool EnableAsync(Plugin plugin)
    {
        if (!_plugins.Contains(plugin) ||
            plugin.State is not (PluginState.Loaded or PluginState.Disabled))
        {
            return false;
        }

        try
        {
            _services.Add(plugin);
        }
        catch (Exception)
        {
            _services.Remove(plugin);
            plugin.State = PluginState.Failed;
            return false;
        }

        plugin.State = PluginState.Enabled;
        return true;
    }

    /// <summary>停用单个插件（须已启用）：移出组合并置 Disabled。</summary>
    public bool DisableAsync(Plugin plugin)
    {
        if (!_plugins.Contains(plugin))
        {
            return false;
        }

        if (plugin.State != PluginState.Enabled)
        {
            return false;
        }

        _services.Remove(plugin);
        plugin.State = PluginState.Disabled;
        return true;
    }

    /// <summary>按依赖图拓扑序启用全部已加载插件；任一失败 → 逆序回滚并返回 false。</summary>
    public bool EnableAsync()
    {
        var enabled = new List<Plugin>();
        foreach (Plugin plugin in GetDependencyOrderedPlugins())
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

    /// <summary>按依赖图逆拓扑序停用全部已加载插件。</summary>
    public bool DisableAsync()
    {
        List<Plugin> order = GetDependencyOrderedPlugins();
        for (int i = order.Count - 1; i >= 0; i--)
        {
            if (!DisableAsync(order[i]))
            {
                return false;
            }
        }

        return true;
    }

    private List<Plugin> GetDependencyOrderedPlugins()
    {
        var byName = _plugins.ToDictionary(p => p.Name, StringComparer.Ordinal);
        return PluginDependencyGraph.Order(_plugins.Select(p => p.Info))
            .Select(i => byName[i.Name])
            .ToList();
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
}
