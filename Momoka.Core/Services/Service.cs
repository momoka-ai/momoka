namespace Momoka.Core.Services;

/// <summary>一次服务注册的记录：提供商实例 + 来源（通常为声明它的插件实例）。</summary>
public sealed record ServiceRegistration<T>(T Provider, object? Source)
    where T : class;

/// <summary>
/// 服务注册表（泛型静态，与 Event&lt;T&gt; 同构）：每服务类型一张静态表（复制写 + volatile 发布，
/// 解析热路径无锁直读）。语义：首个注册成为当前提供商（先到先得），后续注册作为可选提供商
/// （fallback）保留；<see cref="Register"/> 显式覆盖当前提供商（原当前降为 fallback）。
/// 卸载按 <see cref="Remove"/> 整组移除来源注册，当前被移除后自动提升首个可选提供商。
/// </summary>
public static class Service<T>
    where T : class
{
    private static readonly object Gate = new();
    private static volatile ServiceRegistration<T>[] _entries = Array.Empty<ServiceRegistration<T>>();

    /// <summary>当前服务提供商（先到先得或最近一次显式覆盖）；无注册返回 null。</summary>
    public static T? Current
    {
        get
        {
            ServiceRegistration<T>[] entries = _entries;
            return entries.Length > 0 ? entries[0].Provider : null;
        }
    }

    /// <summary>当前提供商注册记录（含来源）；无注册返回 null。</summary>
    public static ServiceRegistration<T>? CurrentRegistration
    {
        get
        {
            ServiceRegistration<T>[] entries = _entries;
            return entries.Length > 0 ? entries[0] : null;
        }
    }

    /// <summary>解析当前服务提供商；无注册抛 <see cref="InvalidOperationException"/>。</summary>
    public static T Resolve()
        => Current ?? throw new InvalidOperationException($"No service of type '{typeof(T)}' is registered.");

    /// <summary>解析当前服务提供商；无注册返回 null。</summary>
    public static T? TryResolve() => Current;

    /// <summary>全部提供商（当前在前，其余按注册先后），即"当前 + 可选提供商"枚举。</summary>
    public static IReadOnlyList<T> All
    {
        get
        {
            ServiceRegistration<T>[] entries = _entries;
            var providers = new T[entries.Length];
            for (int i = 0; i < entries.Length; i++)
            {
                providers[i] = entries[i].Provider;
            }

            return providers;
        }
    }

    /// <summary>全部注册记录快照（当前在前；数组只读，发布后不再改动）。</summary>
    public static IReadOnlyList<ServiceRegistration<T>> Registrations => _entries;

    /// <summary>
    /// 先到先得注册：无当前提供商时登记为当前并返回 true；已有提供商时登记为可选提供商（fallback）
    /// 并返回 false。重复注册同一实例为 no-op。
    /// </summary>
    public static bool TryRegister(T provider, object? source = null)
    {
        ArgumentNullException.ThrowIfNull(provider);
        lock (Gate)
        {
            if (_entries.Length == 0)
            {
                _entries = new[] { new ServiceRegistration<T>(provider, source) };
                return true;
            }

            if (_entries.Any(e => ReferenceEquals(e.Provider, provider)))
            {
                return false;
            }

            _entries = _entries.Append(new ServiceRegistration<T>(provider, source)).ToArray();
            return false;
        }
    }

    /// <summary>显式注册为当前提供商（覆盖原当前，原当前降为可选提供商）；同实例去重。</summary>
    public static void Register(T provider, object? source = null)
    {
        ArgumentNullException.ThrowIfNull(provider);
        lock (Gate)
        {
            var entries = _entries.Where(e => !ReferenceEquals(e.Provider, provider)).ToList();
            entries.Insert(0, new ServiceRegistration<T>(provider, source));
            _entries = entries.ToArray();
        }
    }

    /// <summary>按来源整组移除注册（来源通常为插件实例）；返回移除条数。</summary>
    public static int Remove(object source)
    {
        ArgumentNullException.ThrowIfNull(source);
        lock (Gate)
        {
            ServiceRegistration<T>[] entries = _entries;
            var kept = entries.Where(e => !ReferenceEquals(e.Source, source)).ToArray();
            int removed = entries.Length - kept.Length;
            if (removed > 0)
            {
                _entries = kept;
            }

            return removed;
        }
    }
}
