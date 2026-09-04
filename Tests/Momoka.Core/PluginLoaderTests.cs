using Momoka.Core.Events;
using Momoka.Core.Plugins;
using Momoka.Core.Services;
using Xunit;
using System;
using System.Linq;

namespace Momoka.Core.Tests;

/// <summary>
/// PluginLoader 生命周期（经 PluginService 组合）：Enable 事件监听器可派发、服务可解析且 Singleton
/// 身份保持／Disable 移出组合（不可解析）／Re-Enable 复用旧单例实例／批量启停按依赖拓扑序。
/// </summary>
public sealed class PluginLoaderTests
{
    private interface IShared<T>
    {
    }

    private sealed class SharedImpl<T> : IShared<T>
    {
    }

    private sealed class A
    {
    }

    private sealed class B
    {
    }

    [Fact]
    public void Enable_RegistersEventHandlers_DisableUnregisters()
    {
        var services = new PluginService();
        var loader = new PluginLoader(services);
        var listener = new CountListener<E1>();
        Plugin plugin = loader.RegisterPlugin(Info("p1"), ctx => ctx.AddEventHandler(listener));

        Assert.Equal(PluginState.Loaded, plugin.State);
        Assert.True(loader.EnableAsync(plugin));
        Assert.Equal(PluginState.Enabled, plugin.State);

        services.Events.Send(new E1());
        Assert.Equal(1, listener.Count);

        Assert.True(loader.DisableAsync(plugin));
        Assert.Equal(PluginState.Disabled, plugin.State);
        services.Events.Send(new E1());
        Assert.Equal(1, listener.Count);
    }

    [Fact]
    public void Enable_ServiceResolvable_GetByServiceFindsProvider()
    {
        var services = new PluginService();
        var loader = new PluginLoader(services);
        var impl = new SharedImpl<A>();
        Plugin plugin = loader.RegisterPlugin(Info("provider"), ctx => ctx.AddSingleton<IShared<A>>(impl));

        Assert.Null(services.Resolve<IShared<A>>()); // Enable 前不可解析

        Assert.True(loader.EnableAsync(plugin));

        Assert.Same(impl, services.Resolve<IShared<A>>());
        Assert.Same(plugin, services.GetByService(typeof(IShared<A>)));
        Assert.Same(plugin, services.GetByService<IShared<A>>());
    }

    [Fact]
    public void Disable_MakesUnresolvable_ReEnable_ReusesSameSingleton()
    {
        var services = new PluginService();
        var loader = new PluginLoader(services);
        var impl = new SharedImpl<A>();
        Plugin plugin = loader.RegisterPlugin(Info("reuse"), ctx => ctx.AddSingleton<IShared<A>>(impl));

        Assert.True(loader.EnableAsync(plugin));
        object first = services.Resolve<IShared<A>>()!;

        Assert.True(loader.DisableAsync(plugin));
        Assert.Null(services.Resolve<IShared<A>>());       // 移出组合

        Assert.True(loader.EnableAsync(plugin));
        Assert.Same(first, services.Resolve<IShared<A>>()); // 复用旧单例（描述符 ValueGetter 惰性保持）
    }

    [Fact]
    public void Transient_ResolveCreatesNewEachTime()
    {
        var services = new PluginService();
        var loader = new PluginLoader(services);
        Plugin plugin = loader.RegisterPlugin(Info("trx"), ctx => ctx.AddTransient<IShared<A>, SharedImpl<A>>());

        loader.EnableAsync(plugin);

        Assert.NotSame(services.Resolve<IShared<A>>(), services.Resolve<IShared<A>>());
    }

    [Fact]
    public void EnableAll_TopologyOrder_ThenDisableAll()
    {
        var services = new PluginService();
        var loader = new PluginLoader(services);
        var impl = new SharedImpl<A>();
        Plugin provider = loader.RegisterPlugin(Info("provider"), ctx => ctx.AddSingleton<IShared<A>>(impl));
        Plugin consumer = loader.RegisterPlugin(Info("consumer", dependency: "provider"), _ => { });

        Assert.True(loader.EnableAsync());
        Assert.Equal(PluginState.Enabled, consumer.State);
        Assert.Same(impl, services.Resolve<IShared<A>>());

        Assert.True(loader.DisableAsync());
        Assert.Equal(PluginState.Disabled, consumer.State);
        Assert.Equal(PluginState.Disabled, provider.State);
        Assert.Null(services.Resolve<IShared<A>>());
    }

    [Fact]
    public void RegisterPlugin_DuplicateName_Fails()
    {
        var loader = new PluginLoader(new PluginService());
        loader.RegisterPlugin(Info("dup"), _ => { });

        Assert.Throws<InvalidPluginException>(() => loader.RegisterPlugin(Info("dup"), _ => { }));
    }

    [Fact]
    public void Load_FromAssemblyPath_ResolvesManifestAndStaticBuild()
    {
        var services = new PluginService();
        var loader = new PluginLoader(services);
        string selfPath = typeof(SelfPluginEntry).Assembly.Location;

        Plugin plugin = loader.Load(selfPath);

        Assert.Equal("self", plugin.Name);
        Assert.Equal(PluginState.Loaded, plugin.State);
        Assert.True(loader.EnableAsync(plugin));
        Assert.Same(plugin, services.GetByService(typeof(ISelfServiceMarker)));
        Assert.NotNull(services.Resolve<ISelfServiceMarker>());
        Assert.True(loader.DisableAsync(plugin));
        Assert.Null(services.Resolve<ISelfServiceMarker>());
    }

    private static PluginInfo Info(string name, string? dependency = null)
        => new()
        {
            Name = name,
            Version = "1.0.0",
            Main = $"{name}.Entry, Fake",
            Dependency = dependency is null
                ? Array.Empty<string>()
                : new[] { dependency },
        };

    private sealed record class E1 : Event;

    private sealed class CountListener<TEvent> : IEventHandler
        where TEvent : Event
    {
        public int Count;

        [EventHandler]
        public void OnEvent(TEvent _) => Count++;
    }
}
