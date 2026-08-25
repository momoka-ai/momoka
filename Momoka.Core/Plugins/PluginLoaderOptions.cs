namespace Momoka.Core.Plugins;

/// <summary>插件宿主目录选项：插件目录 / 宿主配置目录 / 插件数据目录。</summary>
public sealed class PluginLoaderOptions
{
    /// <summary>创建目录选项。</summary>
    public PluginLoaderOptions(
        DirectoryInfo pluginDirectory,
        DirectoryInfo configDirectory,
        DirectoryInfo dataDirectory)
    {
        PluginDirectory = pluginDirectory ?? throw new ArgumentNullException(nameof(pluginDirectory));
        ConfigDirectory = configDirectory ?? throw new ArgumentNullException(nameof(configDirectory));
        DataDirectory = dataDirectory ?? throw new ArgumentNullException(nameof(dataDirectory));
    }

    /// <summary>插件目录（递归扫描 *.dll，每插件一子目录）。</summary>
    public DirectoryInfo PluginDirectory { get; }

    /// <summary>宿主配置目录（plugins.toml 插件管理 + plugins/&lt;name&gt;.toml 插件配置）。</summary>
    public DirectoryInfo ConfigDirectory { get; }

    /// <summary>插件数据目录（plugins/&lt;name&gt;/ 插件数据）。</summary>
    public DirectoryInfo DataDirectory { get; }

    /// <summary>
    /// 基于指定基目录的默认选项：&lt;base&gt;/Plugins、&lt;base&gt;/Config、&lt;base&gt;/Data。
    /// 基目录缺省为 <see cref="AppContext.BaseDirectory"/>。
    /// </summary>
    public static PluginLoaderOptions CreateDefault(string? baseDirectory = null)
    {
        string baseDir = baseDirectory ?? AppContext.BaseDirectory;
        return new PluginLoaderOptions(
            new DirectoryInfo(Path.Combine(baseDir, "Plugins")),
            new DirectoryInfo(Path.Combine(baseDir, "Config")),
            new DirectoryInfo(Path.Combine(baseDir, "Data")));
    }
}
