namespace Momoka.Core.Registry;

/// <summary>
/// 线程安全的服务注册表实现：Dictionary&lt;Type, object&gt; + lock；
/// 重复注册 fail-fast，注册时校验实例类型可赋值性。
/// </summary>
public sealed class ServiceRegistry : IServiceRegistry
{
    private readonly object _gate = new();
    private readonly Dictionary<Type, object> _services = new();

    /// <inheritdoc />
    public void Register<TService>(TService instance) where TService : class
    {
        ArgumentNullException.ThrowIfNull(instance);
        Register(typeof(TService), instance);
    }

    /// <inheritdoc />
    public void Register(Type serviceType, object instance)
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
            if (!_services.TryAdd(serviceType, instance))
            {
                throw new InvalidOperationException($"Service of type '{serviceType}' is already registered.");
            }
        }
    }

    /// <inheritdoc />
    public TService Resolve<TService>() where TService : class
    {
        TService? service = TryResolve<TService>();
        if (service is null)
        {
            throw new InvalidOperationException($"No service of type '{typeof(TService)}' is registered.");
        }

        return service;
    }

    /// <inheritdoc />
    public TService? TryResolve<TService>() where TService : class
    {
        lock (_gate)
        {
            return _services.TryGetValue(typeof(TService), out var service) ? (TService)service : null;
        }
    }

    /// <inheritdoc />
    public bool IsRegistered(Type serviceType)
    {
        ArgumentNullException.ThrowIfNull(serviceType);
        lock (_gate)
        {
            return _services.ContainsKey(serviceType);
        }
    }
}
