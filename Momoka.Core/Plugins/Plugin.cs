using Momoka.Core.Commands;
using Momoka.Core.Events;
using Momoka.Core.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using EventHandler = Momoka.Core.Events.EventHandler;

namespace Momoka.Core.Plugins;

/// <summary>
/// 插件声明面（宿主经 plugin.toml 声明的静态 Build 入口填充）：插件只做声明、不控制生命周期——
/// 生命周期完全由宿主（加载/启用/停用）接管，当前阶段见 <see cref="State"/>。
/// 声明项：服务（<see cref="AddSingleton{TService}(TService)"/> 等，仅登记 Service 描述符，
/// 宿主启用时随插件进入组合）／指令（<see cref="Commands"/>）／事件监听器
/// （<see cref="EventHandlers"/>，宿主启用时注册进事件总线）。
/// </summary>
public sealed record Plugin(PluginInfo Info)
{
    /// <summary>插件名（全局唯一，与 manifest.name 一致）。</summary>
    public string Name => Info.Name;

    /// <summary>插件版本（与 manifest.version 一致）。</summary>
    public string Version => Info.Version;

    /// <summary>当前生命周期状态（由宿主启停时更新，初始 Loaded）。</summary>
    public PluginState State { get; set; } = PluginState.Loaded;

    /// <summary>已声明的指令。</summary>
    public List<Command> Commands { get; } = new();

    /// <summary>已装配的事件监听条目（= 监听器上每个 [EventHandler] 方法一条）。</summary>
    public List<EventHandler> EventHandlers { get; } = new();

    /// <summary>已登记的服务描述符（staged：Build 期只收集，Enable 才生效）。</summary>
    public List<Service> Services { get; } = new();

    /// <summary>登记单例服务：直接给定实例（ValueGetter 恒返回它）。</summary>
    public Plugin AddSingleton<TService>(TService instance)
        where TService : class
        => AddService(ServiceLifecycle.Singleton, typeof(TService), typeof(TService), () => instance);

    /// <summary>登记单例服务：惰性工厂（首次取用后共享同一实例）。</summary>
    public Plugin AddSingleton<TService>(Func<TService> factory)
        where TService : class
        => AddService(ServiceLifecycle.Singleton, typeof(TService), typeof(TService), () => factory()!);

    /// <summary>登记单例服务：实现类型（无参构造，容器按需创建一次）。</summary>
    public Plugin AddSingleton<TService, TImpl>()
        where TService : class
        where TImpl : TService, new()
        => AddService(ServiceLifecycle.Singleton, typeof(TService), typeof(TImpl), () => new TImpl());

    /// <summary>登记瞬态服务：惰性工厂（每次取用都新建）。</summary>
    public Plugin AddTransient<TService>(Func<TService> factory)
        where TService : class
        => AddService(ServiceLifecycle.Transient, typeof(TService), typeof(TService), () => factory()!);

    /// <summary>登记瞬态服务：实现类型（无参构造，每次取用新建）。</summary>
    public Plugin AddTransient<TService, TImpl>()
        where TService : class
        where TImpl : TService, new()
        => AddService(ServiceLifecycle.Transient, typeof(TService), typeof(TImpl), () => new TImpl());

    private Plugin AddService(ServiceLifecycle lifecycle, Type sourceType, Type targetType, Func<object> factory)
    {
        Services.Add(new Service(lifecycle, sourceType, targetType, lifecycle.ToValueGetter(factory), this));
        return this;
    }

    /// <summary>声明指令（记录归属 <see cref="Command.Source"/>）。</summary>
    public Plugin AddCommand(Command command)
    {
        command.Source = this;
        Commands.Add(command);
        return this;
    }

    public Plugin AddCommand(Command[] commands)
    {
        foreach (Command command in commands)
        {
            AddCommand(command);
        }

        return this;
    }

    /// <summary>
    /// 声明事件监听器（实现 <see cref="IEventHandler"/>，方法上标记 <see cref="EventHandlerAttribute"/>）：
    /// 反射扫描并把每个带标记方法封装为一条 <see cref="EventHandler"/> 记录
    /// （事件类型 = 方法参数，须为 <see cref="Event"/> 派生类型；优先级取自特性）。
    /// 同一监听器对象整体只装配一次（幂等）。
    /// </summary>
    public Plugin AddEventHandler(IEventHandler handlers)
    {
        if (EventHandlers.Any(h => ReferenceEquals(h.Owner, handlers)))
        {
            return this;
        }

        Type type = handlers.GetType();
        foreach (var item in type
            .GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
            .Where(x =>
                x.GetCustomAttribute<EventHandlerAttribute>() is not null &&
                x.GetParameters().Length == 1 &&
                typeof(Event).IsAssignableFrom(x.GetParameters()[0].ParameterType))
            .Where(x => x.ReturnType == typeof(void))
            .Select(x => (
                Method: x,
                x.GetParameters()[0].ParameterType,
                x.GetCustomAttribute<EventHandlerAttribute>()!.Priority)))
        {
            try
            {
                Delegate action = item.Method
                    .CreateDelegate(typeof(Action<>)
                    .MakeGenericType(item.ParameterType), handlers);

                typeof(Plugin).GetMethod(nameof(AddEventHandlerBinding))!
                    .MakeGenericMethod(item.ParameterType)
                    .Invoke(this, [handlers, item.Method, item.Priority]);
            }
            catch (Exception)
            {
                // 尽力而为的扫描：CreateDelegate 对不兼容签名（如 ref/out、泛型方法等）会抛异常，
                // 该条跳过、继续扫描其余 [EventHandler] 方法——单条装配失败不应使整批监听器声明失败。
            }
        }

        return this;
    }

    /// <summary>装配单条类型化监听条目（事件类型 = 方法参数类型）。</summary>
    public Plugin AddEventHandlerBinding<T>(IEventHandler owner, MethodInfo method, EventPriority priority)
        where T : Event
    {
        var typed = (Action<T>)method.CreateDelegate(typeof(Action<T>), owner);
        EventHandlers.Add(new(owner, typeof(T), @event => typed((T)@event), priority));

        return this;
    }
}
