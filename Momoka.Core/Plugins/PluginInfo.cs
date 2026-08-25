using Tomlyn;
using Tomlyn.Serialization;

namespace Momoka.Core.Plugins;

/// <summary>
/// 插件信息：manifest 字段（name/version/main/dependency/dependencyOptional/authors/description/api，
/// 由 plugin.toml 直接反序列化）+ 运行时字段（生命周期状态）。一个程序集 = 一个插件。
/// </summary>
public sealed class PluginInfo
{
    /// <summary>插件名（全局唯一）。</summary>
    [TomlRequired]
    public string Name { get; set; } = string.Empty;

    /// <summary>插件版本（SemVer 风格，保留字符串原样记录与展示）。</summary>
    [TomlRequired]
    public string Version { get; set; } = string.Empty;

    /// <summary>入口类型全名（<c>CorePlugin</c> 子类），格式 <c>TypeFullName, AssemblyName</c>。
    /// 程序集加载后惰性解析，故类型为字符串而非 <see cref="System.Type"/>。</summary>
    [TomlRequired]
    public string Main { get; set; } = string.Empty;

    /// <summary>硬前置依赖插件名数组（可选，默认空）：引用未知或禁用插件 → fail-fast。</summary>
    [TomlPropertyName("dependency")]
    public IReadOnlyList<string> Dependency { get; set; } = Array.Empty<string>();

    /// <summary>软前置依赖插件名数组（可选，默认空）：目标缺失或禁用时静默跳过；
    /// 目标存在并启用则与 <see cref="Dependency"/> 一样参与加载排序。</summary>
    [TomlPropertyName("dependencyOptional")]
    public IReadOnlyList<string> DependencyOptional { get; set; } = Array.Empty<string>();

    /// <summary>插件作者与贡献者（可选，默认空）。</summary>
    public string[] Authors { get; set; } = Array.Empty<string>();

    /// <summary>插件可读描述（可选，默认空）。</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>插件开发时针对的宿主 API 版本（可选，默认 1.0）。</summary>
    [TomlPropertyName("api")]
    public Version Api { get; set; } = new(1, 0);

    /// <summary>当前生命周期状态（由宿主推进）。</summary>
    [TomlIgnore]
    public CorePlugin.PluginState State { get; internal set; } = CorePlugin.PluginState.Discovered;

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
            || string.IsNullOrWhiteSpace(info.Main))
        {
            throw new InvalidInfoException($"Plugin manifest '{sourceName}' is missing required field.");
        }

        return info;
    }
}

/// <summary>
/// 插件依赖图纯函数：校验（重复名 / 未知或禁用的硬前置 / 依赖环）与拓扑排序。
/// 硬前置（<see cref="PluginInfo.Dependency"/>）缺失或禁用 → fail-fast；
/// 软前置（<see cref="PluginInfo.DependencyOptional"/>）不可解析时静默跳过，可解析则同样构成排序边。
/// 排序结果即 Load / Start 顺序；校验失败全部 fail-fast（抛
/// <see cref="InvalidPluginException"/> / <see cref="UnknownDependencyException"/>）。
/// </summary>
internal static class PluginDependencyGraph
{
    /// <summary>
    /// 过滤禁用插件后按依赖拓扑排序。禁用插件被跳过（其依赖不参与校验）；
    /// 启用插件的硬前置引用未知或禁用插件 → <see cref="UnknownDependencyException"/>；
    /// 软前置仅在被引用插件存在且启用时作为排序边。
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

            foreach (var dependency in plugin.Dependency)
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

        return TopologicalSort(enabled, byName, disabledNames);
    }

    private static List<PluginInfo> TopologicalSort(
        IReadOnlyList<PluginInfo> enabled,
        Dictionary<string, PluginInfo> byName,
        IReadOnlySet<string> disabledNames)
    {
        var remainingDependencies = new Dictionary<string, int>(StringComparer.Ordinal);
        var dependents = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var plugin in enabled)
        {
            var edges = new List<string>(plugin.Dependency);
            edges.AddRange(plugin.DependencyOptional.Where(
                d => byName.ContainsKey(d) && !disabledNames.Contains(d)));

            remainingDependencies[plugin.Name] = edges.Count;
            foreach (var dependency in edges)
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
