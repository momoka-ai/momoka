using Tomlyn;
using Tomlyn.Model;

namespace Momoka.Core.Plugins;

/// <summary>
/// 插件只读内嵌元数据（plugin.toml）：仅 <c>name</c> / <c>version</c> / <c>entry</c> /
/// <c>dependsOn</c>。运行态与可写内容（settings / enabled）一律不进 manifest——
/// <c>enabled</c> 走宿主插件管理配置（config/plugins.toml），插件设置走
/// <c>GetPluginConfig()</c>，数据走 <c>GetPluginFolder()</c>。
/// </summary>
public sealed class PluginManifest
{
    private PluginManifest(string name, string version, string entry, IReadOnlyList<string> dependsOn)
    {
        Name = name;
        Version = version;
        Entry = entry;
        DependsOn = dependsOn;
    }

    /// <summary>插件名（全局唯一）。</summary>
    public string Name { get; }

    /// <summary>插件版本。</summary>
    public string Version { get; }

    /// <summary>入口类型全名（<c>CorePlugin</c> 子类），格式 <c>TypeFullName, AssemblyName</c>。</summary>
    public string Entry { get; }

    /// <summary>依赖插件名数组（可选，默认空）。</summary>
    public IReadOnlyList<string> DependsOn { get; }

    /// <summary>
    /// 解析 TOML 文本为 manifest；语法错误 / 缺必填字段 / 类型不符抛
    /// <see cref="PluginLoadException"/>（fail-fast）。
    /// </summary>
    public static PluginManifest Parse(string toml, string sourceName)
    {
        ArgumentNullException.ThrowIfNull(toml);
        ArgumentException.ThrowIfNullOrEmpty(sourceName);

        TomlTable table;
        try
        {
            table = TomlSerializer.Deserialize<TomlTable>(
                    toml, new TomlSerializerOptions { SourceName = sourceName })
                ?? throw new PluginLoadException($"Failed to parse plugin manifest '{sourceName}'.");
        }
        catch (TomlException ex)
        {
            throw new PluginLoadException($"Failed to parse plugin manifest '{sourceName}'.", ex);
        }

        string name = ReadRequiredString(table, "name", sourceName);
        string version = ReadRequiredString(table, "version", sourceName);
        string entry = ReadRequiredString(table, "entry", sourceName);
        string[] dependsOn = ReadStringArray(table, "dependsOn", sourceName);

        return new PluginManifest(name, version, entry, dependsOn);
    }

    private static string ReadRequiredString(TomlTable table, string key, string sourceName)
    {
        if (!table.TryGetValue(key, out var value) || value is not string s || string.IsNullOrWhiteSpace(s))
        {
            throw new PluginLoadException($"Plugin manifest '{sourceName}' is missing required field '{key}'.");
        }

        return s;
    }

    private static string[] ReadStringArray(TomlTable table, string key, string sourceName)
    {
        if (!table.TryGetValue(key, out var value))
        {
            return Array.Empty<string>();
        }

        if (value is not TomlArray array)
        {
            throw new PluginLoadException($"Plugin manifest '{sourceName}' field '{key}' must be an array of strings.");
        }

        var result = new List<string>(array.Count);
        foreach (var item in array)
        {
            if (item is not string s || string.IsNullOrWhiteSpace(s))
            {
                throw new PluginLoadException($"Plugin manifest '{sourceName}' field '{key}' must be an array of strings.");
            }

            result.Add(s);
        }

        return result.ToArray();
    }
}

/// <summary>
/// 插件依赖图纯函数：校验（重复名 / 未知依赖 / 禁用依赖 / 依赖环）与拓扑排序。
/// 排序结果即 Load / Start 顺序；校验失败全部 fail-fast（抛 <see cref="PluginLoadException"/>）。
/// </summary>
internal static class PluginDependencyGraph
{
    /// <summary>
    /// 过滤禁用插件后按依赖拓扑排序。禁用插件被跳过（其依赖不参与校验）；
    /// 启用插件的 dependsOn 引用未知或禁用插件 → fail-fast。
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
                throw new PluginLoadException($"Duplicate plugin name '{plugin.Name}'.");
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
                    throw new PluginLoadException(
                        $"Plugin '{plugin.Name}' depends on unknown plugin '{dependency}'.");
                }

                if (disabledNames.Contains(dependency))
                {
                    throw new PluginLoadException(
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
            throw new PluginLoadException("Cyclic dependency detected among plugins.");
        }

        return sorted;
    }
}
