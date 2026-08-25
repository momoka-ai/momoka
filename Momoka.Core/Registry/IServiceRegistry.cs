namespace Momoka.Core.Registry;

/// <summary>
/// 插件间服务发现表（每类型单实例，Type 为不透明键）。
/// 与 DI 容器分工：宿主自身设施走 DI；插件提供的业务服务走本注册表
/// （插件经反射实例化，无法构造器注入）。
/// </summary>
public interface IServiceRegistry
{
    /// <summary>按 <typeparamref name="TService"/> 注册单实例；重复注册 fail-fast。</summary>
    void Register<TService>(TService instance) where TService : class;

    /// <summary>按类型注册单实例；实例必须可赋值给 <paramref name="serviceType"/>，否则 fail-fast。</summary>
    void Register(Type serviceType, object instance);

    /// <summary>解析已注册服务；缺失抛 <see cref="InvalidOperationException"/>（fail-fast）。</summary>
    TService Resolve<TService>() where TService : class;

    /// <summary>解析已注册服务；缺失返回 null。</summary>
    TService? TryResolve<TService>() where TService : class;

    /// <summary>是否已注册该服务类型。</summary>
    bool IsRegistered(Type serviceType);
}
