namespace Momoka.Core.Plugins;

/// <summary>已发现插件的静态信息（manifest 数据 + 程序集位置 + 生命周期状态）。</summary>
public sealed class PluginInfo
{
    public PluginInfo(
        string name,
        string version,
        string entry,
        IReadOnlyList<string> dependsOn,
        DirectoryInfo location)
    {
        Name = name;
        Version = version;
        Entry = entry;
        DependsOn = dependsOn;
        Location = location;
    }

    /// <summary>插件名（manifest <c>name</c>）。</summary>
    public string Name { get; }

    /// <summary>插件版本（manifest <c>version</c>）。</summary>
    public string Version { get; }

    /// <summary>入口类型全名（manifest <c>entry</c>，<c>CorePlugin</c> 子类）。</summary>
    public string Entry { get; }

    /// <summary>依赖插件名列表（manifest <c>dependsOn</c>）。</summary>
    public IReadOnlyList<string> DependsOn { get; }

    /// <summary>插件程序集所在目录。</summary>
    public DirectoryInfo Location { get; }

    /// <summary>当前生命周期状态（由宿主推进）。</summary>
    public PluginState State { get; internal set; } = PluginState.Discovered;
}
