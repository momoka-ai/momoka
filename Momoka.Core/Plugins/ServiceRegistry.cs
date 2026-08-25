namespace Momoka.Core.Plugins;

/// <summary>
/// 插件间服务发现表（线程安全）。同类型允许多注册，每项记录来源插件与优先级；
/// 单值解析取优先级最高者（同级按先注册先得）。与 DI 容器分工：
/// 宿主自身设施走 DI；插件提供的业务服务走本注册表（插件经反射实例化，无法构造器注入）。
/// </summary>
public sealed class ServiceRegistry
{
    private readonly object _gate = new();
    private readonly Dictionary<Type, List<ServiceEntry>> _services = new();
    private long _sequence;

    /// <summary>按 <typeparamref name="TService"/> 注册服务实例（可选优先级与来源插件）。</summary>
    public void Register<TService>(
        TService instance,
        ServicePriority priority = ServicePriority.Normal,
        IPlugin? plugin = null)
        where TService : class
    {
        ArgumentNullException.ThrowIfNull(instance);
        Register(typeof(TService), instance, priority, plugin);
    }

    /// <summary>按类型注册服务实例；实例必须可赋值给 <paramref name="serviceType"/>，否则 fail-fast。</summary>
    public void Register(
        Type serviceType,
        object instance,
        ServicePriority priority = ServicePriority.Normal,
        IPlugin? plugin = null)
    {
        ArgumentNullException.ThrowIfNull(serviceType);
        ArgumentNullException.ThrowIfNull(instance);
        if (!serviceType.IsInstanceOfType(instance))
        {
            throw new ArgumentException(
                $"Instance of type '{instance.GetType()}' is not assignable to service type '{serviceType}'.",
                nameof(instance));
        }

        lock (_gate)
        {
            if (!_services.TryGetValue(serviceType, out var list))
            {
                list = new List<ServiceEntry>();
                _services.Add(serviceType, list);
            }

            list.Add(new ServiceEntry(serviceType, instance, priority, plugin, _sequence++));
        }
    }

    /// <summary>解析最高优先级服务实例；无注册抛 <see cref="InvalidOperationException"/>。</summary>
    public TService Resolve<TService>() where TService : class
    {
        TService? service = TryResolve<TService>();
        if (service is null)
        {
            throw new InvalidOperationException($"No service of type '{typeof(TService)}' is registered.");
        }

        return service;
    }

    /// <summary>解析最高优先级服务实例；无注册返回 null。</summary>
    public TService? TryResolve<TService>() where TService : class
    {
        lock (_gate)
        {
            return _services.TryGetValue(typeof(TService), out var list)
                ? (TService)BestEntry(list).Instance
                : null;
        }
    }

    /// <summary>解析最高优先级服务实例；无注册返回 null（<see cref="TryResolve{TService}"/> 别名）。</summary>
    public TService? GetService<TService>() where TService : class => TryResolve<TService>();

    /// <summary>解析最高优先级服务实例（out 变体）；无注册返回 false。</summary>
    public bool TryGetService<TService>(out TService? value) where TService : class
    {
        value = TryResolve<TService>();
        return value is not null;
    }

    /// <summary>按 <typeparamref name="TService"/> 枚举全部注册（按优先级降序、同级按注册先后）。</summary>
    public IEnumerable<ServiceSource<TService>> GetRegistrations<TService>() where TService : class
        => GetRegistrations<TService>(typeof(TService));

    /// <summary>按指定运行期注册键枚举注册（实例转型为 <typeparamref name="TService"/>，通常为其基类/接口）。</summary>
    public IEnumerable<ServiceSource<TService>> GetRegistrations<TService>(Type serviceType)
        where TService : class
    {
        ArgumentNullException.ThrowIfNull(serviceType);

        List<ServiceEntry> snapshot;
        lock (_gate)
        {
            _services.TryGetValue(serviceType, out var list);
            snapshot = list is null ? new List<ServiceEntry>() : list.ToList();
        }

        return snapshot
            .Where(e => e.Instance is TService)
            .OrderBy(e => e.Priority)
            .ThenBy(e => e.Sequence)
            .Select(e => new ServiceSource<TService>(
                serviceType, (TService)e.Instance, e.Priority, e.Plugin));
    }

    /// <summary>枚举指定来源插件注册的全部服务。</summary>
    public IEnumerable<ServiceSource<TService>> GetRegistrations<TService>(IPlugin plugin)
        where TService : class
    {
        ArgumentNullException.ThrowIfNull(plugin);
        lock (_gate)
        {
            return _services.Values
                .SelectMany(list => list)
                .Where(e => e.Plugin == plugin && e.Instance is TService)
                .OrderBy(e => e.Priority)
                .ThenBy(e => e.Sequence)
                .Select(e => new ServiceSource<TService>(
                    e.ServiceType, (TService)e.Instance, e.Priority, e.Plugin!))
                .ToList();
        }
    }

    /// <summary>该类型是否已有注册。</summary>
    public bool IsRegistered(Type serviceType)
    {
        ArgumentNullException.ThrowIfNull(serviceType);
        lock (_gate)
        {
            return _services.ContainsKey(serviceType);
        }
    }

    private static ServiceEntry BestEntry(IReadOnlyList<ServiceEntry> list)
    {
        ServiceEntry best = list[0];
        for (int i = 1; i < list.Count; i++)
        {
            if (list[i].Priority < best.Priority)
            {
                best = list[i];
            }
        }

        return best;
    }

    private sealed class ServiceEntry
    {
        public ServiceEntry(Type serviceType, object instance, ServicePriority priority, IPlugin? plugin, long sequence)
        {
            ServiceType = serviceType;
            Instance = instance;
            Priority = priority;
            Plugin = plugin;
            Sequence = sequence;
        }

        public Type ServiceType { get; }

        public object Instance { get; }

        public ServicePriority Priority { get; }

        public IPlugin? Plugin { get; }

        public long Sequence { get; }
    }
}
