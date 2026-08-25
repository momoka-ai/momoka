namespace Momoka.Core.Plugins;

/// <summary>一次服务注册的记录：注册键类型 / 实例 / 优先级 / 来源插件。</summary>
public readonly record struct ServiceSource<T>(
    Type Service,
    T Source,
    ServicePriority Priority,
    Plugin? Plugin
);
