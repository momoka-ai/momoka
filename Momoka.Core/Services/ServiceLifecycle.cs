using System;
using System.Threading;

namespace Momoka.Core.Services;

/// <summary>服务生命周期：Singleton 随描述符惰性共享实例；Scoped 需外部工作单元（暂不支持）；Transient 每次取用新建。</summary>
public enum ServiceLifecycle
{
    Singleton,
    Scoped,
    Transient
}

/// <summary>
/// <see cref="ServiceLifecycle"/> 的取用器构造扩展：把"裸构造工厂"按生命周期封装成
/// <see cref="Service"/> 记录里可直接调用的 ValueGetter。Singleton 经 Lazy 惰性单例化（线程安全），
/// Transient 原样返回；Scoped 需要外部工作单元，目前不支持（抛 NotSupportedException）。
/// </summary>
public static class ServiceLifecycleExtensions
{
    public static Func<object> ToValueGetter(this ServiceLifecycle lifecycle, Func<object> factory)
        => lifecycle switch
        {
            ServiceLifecycle.Transient => factory,
            ServiceLifecycle.Singleton => Singleton(factory),
            _ => throw new NotSupportedException(
                $"Lifecycle '{lifecycle}' needs an external scope to resolve; not yet supported."),
        };

    private static Func<object> Singleton(Func<object> factory)
    {
        Lazy<object> value = new(factory, LazyThreadSafetyMode.ExecutionAndPublication);
        return () => value.Value;
    }
}
