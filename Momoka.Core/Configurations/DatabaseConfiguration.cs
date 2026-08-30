using System.Globalization;
using System.Text.Json;

namespace Momoka.Core.Configurations;

/// <summary>
/// 数据库配置：经 <see cref="IConfigurationStore"/>（扁平行）存取配置值树 + 版本。
/// 行值类型嗅探解释（<c>true/false</c> → bool，整数 → long，浮点 → double，<c>[...]</c> → 值列表，
/// 其余为字符串；存储层不解释语义）。顶层保留键 <c>version</c> 记录配置版本。
/// </summary>
public sealed class DatabaseConfiguration : Configuration
{
    /// <summary>顶层保留键：记录配置版本（行式保存 / 加载时读写）。</summary>
    public const string VersionKey = "version";

    private readonly IConfigurationStore _store;

    /// <summary>创建数据库配置并立即 <see cref="Reload"/>（读行 + 迁移链升级）。</summary>
    public DatabaseConfiguration(
        IConfigurationStore store,
        IEnumerable<Migration>? migrations = null,
        Version? targetVersion = null)
        : base(migrations, targetVersion)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        Reload();
    }

    /// <summary>重读存储行（含迁移链重新应用）；迁移断链 / 版本非法抛 <see cref="ConfigurationException"/>。</summary>
    public void Reload()
    {
        IReadOnlyDictionary<string, string?> rows = _store.ReadAll();
        var tree = new Dictionary<string, object?>(StringComparer.Ordinal);
        string stored = "1.0";
        foreach (var (key, raw) in rows)
        {
            if (string.IsNullOrWhiteSpace(key) || raw is null)
            {
                continue;
            }

            if (key == VersionKey)
            {
                stored = raw;
                continue;
            }

            SetPath(tree, key.Split('.'), ParseText(raw));
        }

        if (!Version.TryParse(stored, out Version? version))
        {
            throw new ConfigurationException(
                $"Configuration store has an invalid '{VersionKey}' value '{stored}'.");
        }

        LoadValues(tree, version);
    }

    /// <summary>把当前值树 + 版本整体写回存储（替换全部行）。</summary>
    public void Save()
    {
        var rows = new Dictionary<string, string?>(StringComparer.Ordinal);
        Flatten(string.Empty, SnapshotValues(), rows);
        rows[VersionKey] = Version.ToString();
        _store.WriteAll(rows);
    }

    private static void SetPath(Dictionary<string, object?> root, string[] segments, object? value)
    {
        var node = root;
        for (int i = 0; i < segments.Length - 1; i++)
        {
            if (!node.TryGetValue(segments[i], out object? next)
                || next is not Dictionary<string, object?> dict)
            {
                dict = new Dictionary<string, object?>(StringComparer.Ordinal);
                node[segments[i]] = dict;
            }

            node = dict;
        }

        node[segments[^1]] = value;
    }

    private static void Flatten(string prefix, Dictionary<string, object?> tree, Dictionary<string, string?> rows)
    {
        foreach (var (key, value) in tree)
        {
            string path = prefix.Length == 0 ? key : prefix + "." + key;
            if (value is Dictionary<string, object?> nested)
            {
                Flatten(path, nested, rows);
            }
            else
            {
                rows[path] = ValueToText(value);
            }
        }
    }

    private static string? ValueToText(object? value) => value switch
    {
        null => null,
        bool flag => flag ? "true" : "false",
        long number => number.ToString(CultureInfo.InvariantCulture),
        double number => number.ToString("R", CultureInfo.InvariantCulture),
        DateTime dateTime => dateTime.ToString("O", CultureInfo.InvariantCulture),
        string text => text,
        List<object?> list => JsonSerializer.Serialize(list),
        _ => throw new ConfigurationException(
            $"Value of type '{value.GetType()}' cannot be stored in database configuration."),
    };

    private static object? ParseText(string text)
    {
        if (bool.TryParse(text, out bool flag))
        {
            return flag;
        }

        if (long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out long number))
        {
            return number;
        }

        if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double real))
        {
            return real;
        }

        if (text.Length >= 2 && text[0] == '[' && text[^1] == ']')
        {
            try
            {
                JsonElement element = JsonSerializer.Deserialize<JsonElement>(text);
                if (element.ValueKind == JsonValueKind.Array)
                {
                    return element.EnumerateArray().Select(FromJsonElement).ToList();
                }
            }
            catch (JsonException)
            {
                // 非合法 JSON 数组 → 按字符串回退
            }
        }

        return text;
    }

    private static object? FromJsonElement(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        JsonValueKind.String => element.GetString(),
        JsonValueKind.Number when element.TryGetInt64(out long number) => number,
        JsonValueKind.Number => element.GetDouble(),
        JsonValueKind.Array => element.EnumerateArray().Select(FromJsonElement).ToList(),
        JsonValueKind.Object => element.EnumerateObject()
            .ToDictionary(p => p.Name, p => FromJsonElement(p.Value), StringComparer.Ordinal),
        _ => null,
    };
}
