using Tomlyn;
using Tomlyn.Model;
using Tomlyn.Serialization;

namespace Momoka.Core.Configurations;

/// <summary>
/// 文件配置（TOML）：从 <c>*.toml</c> 文件读取 / 保存配置值树。顶层保留键 <c>version</c>
/// 记录配置版本（预留，不可用作普通键）；加载时按迁移链升级到目标版本，未知字段保留（向后兼容）。
/// </summary>
public sealed class FileConfiguration : Configuration
{
    /// <summary>顶层保留键：记录配置版本（加载 / 保存时读写）。</summary>
    public const string VersionKey = "version";

    private readonly string _path;

    /// <summary>创建文件配置（path 为读写目标；迁移链与目标版本见 <see cref="Configuration"/>）。</summary>
    public FileConfiguration(
        string path,
        IEnumerable<Migration>? migrations = null,
        Version? targetVersion = null)
        : base(migrations, targetVersion)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        _path = path;
    }

    /// <summary>从 <see cref="FileConfiguration"/> 指向的文件加载；文件缺失抛 <see cref="FileNotFoundException"/>，TOML 非法 / 迁移断链抛 <see cref="ConfigurationException"/>。</summary>
    public void Load()
    {
        using var stream = File.OpenRead(_path);
        Load(stream);
    }

    /// <summary>从流加载 TOML 文本（内容按 <see cref="FileConfiguration"/> 的路径名做诊断来源）。</summary>
    public void Load(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        string toml;
        using (var reader = new StreamReader(stream))
        {
            toml = reader.ReadToEnd();
        }

        TomlTable table;
        try
        {
            table = TomlSerializer.Deserialize<TomlTable>(
                toml,
                new TomlSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    SourceName = _path,
                }) ?? new TomlTable();
        }
        catch (Exception ex)
        {
            throw new ConfigurationException($"Failed to parse configuration file '{_path}'.", ex);
        }

        Dictionary<string, object?> tree = TableToTree(table);
        tree.Remove(VersionKey);
        string stored = table.TryGetValue(VersionKey, out object? versionValue) && versionValue is not null
            ? versionValue.ToString()!
            : "1.0";
        if (!Version.TryParse(stored, out Version? storedVersion))
        {
            throw new ConfigurationException(
                $"Configuration file '{_path}' has an invalid '{VersionKey}' value '{stored}'.");
        }

        LoadValues(tree, storedVersion);
    }

    /// <summary>保存到 <see cref="FileConfiguration"/> 指向的文件（自动创建目录与文件）。</summary>
    public void Save() => Save(_path);

    /// <summary>保存到指定路径。</summary>
    public void Save(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        using var stream = File.Create(path);
        Save(stream);
    }

    /// <summary>把当前值树（含版本键）写入流（TOML，缩进格式）。</summary>
    public void Save(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        Dictionary<string, object?> tree = SnapshotValues();
        tree[VersionKey] = Version.ToString();
        using var writer = new StreamWriter(stream);
        TomlSerializer.Serialize(
            writer,
            TableFromTree(tree),
            new TomlSerializerOptions { WriteIndented = true });
        writer.Flush();
    }

    private static Dictionary<string, object?> TableToTree(TomlTable table)
    {
        var result = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var (key, value) in table)
        {
            result[key] = ModelToValue(value);
        }

        return result;
    }

    private static object? ModelToValue(object? value) => value switch
    {
        TomlTable table => TableToTree(table),
        TomlTableArray tableArray => tableArray.Select(TableToTree).Cast<object?>().ToList(),
        TomlArray array => array.Select(ModelToValue).ToList(),
        TomlDateTime dateTime => dateTime.DateTime.DateTime,
        _ => value,
    };

    private static TomlTable TableFromTree(Dictionary<string, object?> tree)
    {
        var table = new TomlTable();
        foreach (var (key, value) in tree)
        {
            table[key] = ModelFromValue(value)!;
        }

        return table;
    }

    private static object? ModelFromValue(object? value) => value switch
    {
        Dictionary<string, object?> nested => TableFromTree(nested),
        List<object?> list => ListFromTree(list),
        DateTime dateTime => (TomlDateTime)dateTime,
        _ => value,
    };

    private static TomlArray ListFromTree(IEnumerable<object?> list)
    {
        var array = new TomlArray();
        foreach (object? item in list)
        {
            array.Add(ModelFromValue(item)!);
        }

        return array;
    }
}
