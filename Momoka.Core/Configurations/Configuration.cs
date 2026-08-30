using System.Collections;
using System.Globalization;

namespace Momoka.Core.Configurations;

/// <summary>
/// 配置统一基类：持有不透明值树（点分路径分层键，值 = 基础类型 / 值列表 / 嵌套表）与版本号，
/// 提供类型化读写与版本迁移链（旧配置**向上升级**、未知字段保留，**向后兼容**）。
/// 存储后端由子类决定（文件 / 二进制 / 数据库）。线程安全，值访问低频可接受整树锁。
/// </summary>
public abstract class Configuration
{
    private readonly object _gate = new();
    private readonly List<Migration> _migrations;
    private readonly Version _targetVersion;
    private Dictionary<string, object?> _values = new(StringComparer.Ordinal);
    private Version _version = new(1, 0);

    /// <summary>创建配置（可选迁移链与目标版本；目标缺省 = 迁移链最大 To，无迁移则为 1.0）。</summary>
    protected Configuration(IEnumerable<Migration>? migrations = null, Version? targetVersion = null)
    {
        _migrations = migrations?.ToList() ?? new List<Migration>();
        Version? duplicateFrom = _migrations
            .GroupBy(m => m.From)
            .FirstOrDefault(g => g.Count() > 1)
            ?.Key;
        if (duplicateFrom is not null)
        {
            throw new ArgumentException(
                $"Duplicate migration source version '{duplicateFrom}'.", nameof(migrations));
        }

        _targetVersion = targetVersion ?? (_migrations.Count == 0
            ? _version
            : _migrations.Max(m => m.To) ?? _version);
    }

    /// <summary>当前配置版本（迁移链应用后）。</summary>
    public Version Version
    {
        get
        {
            lock (_gate)
            {
                return _version;
            }
        }
    }

    /// <summary>路径（点分）是否已有值（含 null 值）。</summary>
    public bool Contains(string path)
    {
        lock (_gate)
        {
            return TryNavigate(_values, path, out _);
        }
    }

    /// <summary>读取并转换路径值；缺失或为 null 抛 <see cref="ConfigurationException"/>。</summary>
    public T Get<T>(string path)
    {
        object? raw;
        lock (_gate)
        {
            if (!TryNavigate(_values, path, out raw) || raw is null)
            {
                throw new ConfigurationException($"Configuration value '{path}' was not found or is null.");
            }
        }

        return ConvertValue<T>(raw, path);
    }

    /// <summary>尝试读取并转换路径值；缺失返回 false。值存在但类型不符抛 <see cref="ConfigurationException"/>。</summary>
    public bool TryGet<T>(string path, out T? value)
    {
        object? raw;
        lock (_gate)
        {
            if (!TryNavigate(_values, path, out raw) || raw is null)
            {
                value = default;
                return false;
            }
        }

        value = ConvertValue<T>(raw, path);
        return true;
    }

    /// <summary>读取路径原始值（存储形态：string/bool/long/double/DateTime/List/嵌套表）；缺失返回 null。</summary>
    public object? GetValue(string path)
    {
        lock (_gate)
        {
            return TryNavigate(_values, path, out object? raw) ? raw : null;
        }
    }

    /// <summary>按路径写值（基础类型 / 枚举 / Guid / 可枚举 / 嵌套字典），自动规整为存储形态。</summary>
    public void Set<T>(string path, T value) => SetValue(path, ToStorageValue(value));

    /// <summary>按路径写原始值（须为存储可表示形态，否则 fail-fast）。</summary>
    public void SetValue(string path, object? value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        lock (_gate)
        {
            string[] segments = path.Split('.');
            var node = _values;
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
    }

    /// <summary>路径（点分）下一级键名集合；路径缺失或非表返回空。</summary>
    public IReadOnlyCollection<string> GetKeys(string path)
    {
        lock (_gate)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return _values.Keys.ToList();
            }

            return TryNavigate(_values, path, out object? node)
                && node is Dictionary<string, object?> dict
                ? dict.Keys.ToList()
                : Array.Empty<string>();
        }
    }

    /// <summary>加载原始值树并执行迁移链（由子类在读取持久化数据后调用）。</summary>
    protected void LoadValues(IReadOnlyDictionary<string, object?> values, Version storedVersion)
    {
        ArgumentNullException.ThrowIfNull(values);
        ArgumentNullException.ThrowIfNull(storedVersion);
        lock (_gate)
        {
            _values = new Dictionary<string, object?>(values, StringComparer.Ordinal);
            _version = storedVersion;
        }

        ApplyMigrations();
    }

    /// <summary>当前值树深拷贝快照（供子类持久化）。</summary>
    protected Dictionary<string, object?> SnapshotValues()
    {
        lock (_gate)
        {
            return Clone(_values);
        }
    }

    /// <summary>按迁移链从当前版本升级到目标版本；断链（缺迁移步）fail-fast。</summary>
    private void ApplyMigrations()
    {
        Version current;
        Version target;
        lock (_gate)
        {
            current = _version;
            target = _targetVersion;
        }

        if (current >= target)
        {
            return;
        }

        int guard = 0;
        while (current < target)
        {
            Migration? migration;
            lock (_gate)
            {
                migration = _migrations.FirstOrDefault(m => m.From == current);
            }

            if (migration is null)
            {
                throw new ConfigurationException(
                    $"Missing migration from version {current} to reach target version {target}.");
            }

            migration.Apply(this);
            lock (_gate)
            {
                _version = current = migration.To;
            }

            if (++guard > _migrations.Count + 1)
            {
                throw new ConfigurationException(
                    $"Migration chain from {current} does not converge to target version {target}.");
            }
        }
    }

    private static bool TryNavigate(Dictionary<string, object?> root, string path, out object? value)
    {
        value = root;
        foreach (string segment in path.Split('.'))
        {
            if (value is not Dictionary<string, object?> dict || !dict.TryGetValue(segment, out value))
            {
                return false;
            }
        }

        return true;
    }

    private static T ConvertValue<T>(object? raw, string path)
    {
        Type target = Nullable.GetUnderlyingType(typeof(T)) ?? typeof(T);
        object? converted;
        if (raw is null)
        {
            if (target.IsValueType)
            {
                throw new ConfigurationException(
                    $"Configuration value '{path}' is null and cannot be converted to '{target.Name}'.");
            }

            converted = null;
        }
        else if (target == typeof(object))
        {
            converted = raw;
        }
        else if (target == typeof(string))
        {
            converted = raw is string text ? text : Convert.ToString(raw, CultureInfo.InvariantCulture);
        }
        else if (target.IsEnum)
        {
            converted = Enum.Parse(target, raw.ToString()!, ignoreCase: true);
        }
        else if (target == typeof(Guid))
        {
            converted = Guid.Parse(raw.ToString()!);
        }
        else if (target == typeof(DateTime))
        {
            converted = raw is DateTime dateTime
                ? dateTime
                : DateTime.Parse(raw.ToString()!, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
        }
        else
        {
            try
            {
                converted = Convert.ChangeType(raw, target, CultureInfo.InvariantCulture);
            }
            catch (Exception ex)
            {
                throw new ConfigurationException(
                    $"Configuration value '{path}' cannot be converted to '{target.Name}'.", ex);
            }
        }

        return (T)converted!;
    }

    private static object? ToStorageValue(object? value)
    {
        if (value is null)
        {
            return null;
        }

        Type type = value.GetType();
        if (type.IsEnum)
        {
            return value.ToString();
        }

        return value switch
        {
            bool or string or DateTime => value,
            byte or sbyte or short or ushort or int or uint or long or ulong =>
                Convert.ToInt64(value, CultureInfo.InvariantCulture),
            float or double or decimal => Convert.ToDouble(value, CultureInfo.InvariantCulture),
            Guid g => g.ToString(),
            Dictionary<string, object?> dict => dict.ToDictionary(
                kv => kv.Key, kv => ToStorageValue(kv.Value), StringComparer.Ordinal),
            IEnumerable enumerable and not string =>
                enumerable.Cast<object?>().Select(ToStorageValue).ToList(),
            _ => throw new ConfigurationException(
                $"Value of type '{type}' cannot be stored in configuration (only primitives, " +
                "enumerables and tables are supported)."),
        };
    }

    private static Dictionary<string, object?> Clone(Dictionary<string, object?> source)
    {
        var clone = new Dictionary<string, object?>(source.Count, StringComparer.Ordinal);
        foreach (var (key, value) in source)
        {
            clone[key] = value switch
            {
                Dictionary<string, object?> nested => Clone(nested),
                List<object?> list => list.Select(CloneElement).ToList(),
                _ => value,
            };
        }

        return clone;
    }

    private static object? CloneElement(object? value) => value switch
    {
        Dictionary<string, object?> nested => Clone(nested),
        List<object?> list => list.Select(CloneElement).ToList(),
        _ => value,
    };
}
