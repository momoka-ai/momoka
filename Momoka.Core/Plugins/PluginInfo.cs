using Tomlyn;
using Tomlyn.Serialization;

namespace Momoka.Core.Plugins;

/// <summary>
/// 插件信息：manifest 字段（name/version/entry/dependsOn，由 plugin.toml 直接反序列化）
/// + 运行时字段（程序集位置 / 生命周期状态）。一个程序集 = 一个插件。
/// </summary>
public sealed class PluginInfo
{
    /// <summary>插件名（全局唯一）。</summary>
    [TomlRequired]
    public string Name { get; set; } = string.Empty;

    /// <summary>插件版本。</summary>
    [TomlRequired]
    public string Version { get; set; } = string.Empty;

    /// <summary>入口类型全名（<c>CorePlugin</c> 子类），格式 <c>TypeFullName, AssemblyName</c>。</summary>
    [TomlRequired]
    public string Entry { get; set; } = string.Empty;

    /// <summary>依赖插件名数组（可选，默认空）。</summary>
    [TomlPropertyName("dependsOn")]
    public IReadOnlyList<string> DependsOn { get; set; } = Array.Empty<string>();

    /// <summary>插件程序集所在目录（运行时回填）。</summary>
    [TomlIgnore]
    public DirectoryInfo Location { get; internal set; } = null!;

    /// <summary>当前生命周期状态（由宿主推进）。</summary>
    [TomlIgnore]
    public PluginState State { get; internal set; } = PluginState.Discovered;

    /// <summary>
    /// 解析 plugin.toml 文本为 <see cref="PluginInfo"/>（Tomlyn 直接反序列化到类型）；
    /// 语法错误 / 缺必填字段 / 类型不符抛 <see cref="InvalidInfoException"/>（fail-fast）。
    /// </summary>
    public static PluginInfo Parse(string toml, string sourceName)
    {
        ArgumentNullException.ThrowIfNull(toml);
        ArgumentException.ThrowIfNullOrEmpty(sourceName);

        PluginInfo info;
        try
        {
            info = TomlSerializer.Deserialize<PluginInfo>(
                    toml, new TomlSerializerOptions { SourceName = sourceName, PropertyNameCaseInsensitive = true })
                ?? throw new InvalidInfoException($"Failed to parse plugin manifest '{sourceName}'.");
        }
        catch (TomlException ex)
        {
            throw new InvalidInfoException($"Failed to parse plugin manifest '{sourceName}'.", ex);
        }

        if (string.IsNullOrWhiteSpace(info.Name)
            || string.IsNullOrWhiteSpace(info.Version)
            || string.IsNullOrWhiteSpace(info.Entry))
        {
            throw new InvalidInfoException($"Plugin manifest '{sourceName}' is missing required field.");
        }

        return info;
    }
}

/// <summary>
/// 插件依赖图纯函数：校验（重复名 / 未知依赖 / 禁用依赖 / 依赖环）与拓扑排序。
/// 排序结果即 Load / Start 顺序；校验失败全部 fail-fast（抛
/// <see cref="InvalidPluginException"/> / <see cref="UnknownDependencyException"/>）。
/// </summary>
internal static class PluginDependencyGraph
{
    /// <summary>
    /// 过滤禁用插件后按依赖拓扑排序。禁用插件被跳过（其依赖不参与校验）；
    /// 启用插件的 dependsOn 引用未知或禁用插件 → <see cref="UnknownDependencyException"/>。
    /// </summary>
    public static List<PluginInfo> Order(IEnumerable<PluginInfo> plugins, IReadOnlySet<string> disabledNames)
    {
        ArgumentNullException.ThrowIfNull(plugins);
        ArgumentNullException.ThrowIfNull(disabledNames);

        var byName = new Dictionary<string, PluginInfo>(StringComparer.Ordinal);
        foreach (var plugin in plugins)
        {
            if (!byName.TryAdd(plugin.Name, plugin))
            {
                throw new InvalidPluginException($"Duplicate plugin name '{plugin.Name}'.");
            }
        }

        var enabled = new List<PluginInfo>();
        foreach (var plugin in plugins)
        {
            if (disabledNames.Contains(plugin.Name))
            {
                continue;
            }

            foreach (var dependency in plugin.DependsOn)
            {
                if (!byName.ContainsKey(dependency))
                {
                    throw new UnknownDependencyException(
                        $"Plugin '{plugin.Name}' depends on unknown plugin '{dependency}'.");
                }

                if (disabledNames.Contains(dependency))
                {
                    throw new UnknownDependencyException(
                        $"Plugin '{plugin.Name}' depends on disabled plugin '{dependency}'.");
                }
            }

            enabled.Add(plugin);
        }

        return TopologicalSort(enabled, byName);
    }

    private static List<PluginInfo> TopologicalSort(
        IReadOnlyList<PluginInfo> enabled,
        IReadOnlyDictionary<string, PluginInfo> byName)
    {
        var remainingDependencies = new Dictionary<string, int>(StringComparer.Ordinal);
        var dependents = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var plugin in enabled)
        {
            remainingDependencies[plugin.Name] = plugin.DependsOn.Count;
            foreach (var dependency in plugin.DependsOn)
            {
                if (!dependents.TryGetValue(dependency, out var list))
                {
                    list = new List<string>();
                    dependents.Add(dependency, list);
                }

                list.Add(plugin.Name);
            }
        }

        var ready = new Queue<PluginInfo>(enabled.Where(p => remainingDependencies[p.Name] == 0));
        var sorted = new List<PluginInfo>(enabled.Count);
        while (ready.Count > 0)
        {
            var plugin = ready.Dequeue();
            sorted.Add(plugin);
            if (!dependents.TryGetValue(plugin.Name, out var dependentsList))
            {
                continue;
            }

            foreach (var dependentName in dependentsList)
            {
                if (--remainingDependencies[dependentName] == 0)
                {
                    ready.Enqueue(byName[dependentName]);
                }
            }
        }

        if (sorted.Count != enabled.Count)
        {
            throw new InvalidPluginException("Cyclic dependency detected among plugins.");
        }

        return sorted;
    }
}
