namespace Momoka.Core.Services;

using System;
using Momoka.Core.Plugins;

/// <summary>
/// 一条服务注册（纯数据）：契约类型 <see cref="SourceType"/> + 实现标识 <see cref="TargetType"/>
/// + 生命周期 <see cref="Lifecycle"/> + 来源插件 <see cref="Plugin"/>。
/// <see cref="ValueGetter"/> 是唯一取值入口，注册时已按生命周期封装完毕：调用即得当前实例，
/// Singleton 首次调用后共享、Transient 每次新建（Scoped 未启用）。
/// </summary>
public record class Service(
    ServiceLifecycle Lifecycle,
    Type SourceType,
    Type TargetType,
    Func<object> ValueGetter,
    Plugin? Plugin);
