using Microsoft.Extensions.Logging.Abstractions;
using Momoka.Core.Events;
using Momoka.Core.Plugins;
using Momoka.Core.Services;
using Xunit;

namespace Momoka.Core.Tests;

/// <summary>
/// PluginLoader 生命周期：Load（manifest + 静态 Build）／Enable（服务注册 → 注入 → 监听器注册）／
/// Disable（消费者守卫 → 反注册 → 按声明移除服务）／批量启停拓扑序。
/// </summary>
public sealed class PluginLoaderTests : IDisposable
{
    private readonly string _tempRoot;

    public PluginLoaderTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), "momoka-core-tests", Guid.NewGuid().ToString("N"));
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_tempRoot, recursive: true);
        }
        catch
        {
            // 清理尽力而为
        }
    }

    private interface IShared<T>
    {
    }

    private interface IConsumer<T>
    {
    }

    private interface IOther<T>
    {
    }

    private sealed class SharedImpl<T> : IShared<T>
    {
    }

    private sealed class ConsumerHolder<T> : IConsumer<T>
    {
        [ServiceInjection]
        public IShared<T> Shared { get; set; } = null!;
    }

    private sealed class OtherImpl<T> : IOther<T>
    {
    }

    private sealed class A
    {
    }

    private sealed class B
    {
    }

    private sealed class C
    {
    }

    [Fact]
    public async Task Enable_RegistersEventHandlers_DisableUnregisters()
    {
        var hub = new EventHub();
        var loader = CreateLoader(hub);
        var listener = new CountListener<E1>();
        Plugin plugin = loader.RegisterPlugin(Info("p1"), ctx => ctx.AddEventHandler(listener));

        Assert.Equal(PluginState.Loaded, loader.GetState(plugin));
        Assert.True(loader.EnableAsync(plugin));
        Assert.Equal(PluginState.Enabled, loader.GetState(plugin));

        await hub.Publish(new E1());
        Assert.Equal(1, listener.Count);

        Assert.True(loader.DisableAsync(plugin));
        Assert.Equal(PluginState.Disabled, loader.GetState(plugin));
        await hub.Publish(new E1());
        Assert.Equal(1, listener.Count);
    }

    [Fact]
    public void EnableAll_TopologyOrder_ServicesResolvable_DisableProviderGuarded()
    {
        var hub = new EventHub();
        var loader = CreateLoader(hub);
        var impl = new SharedImpl<A>();
        var holder = new ConsumerHolder<A>();
        Plugin provider = loader.RegisterPlugin(Info("provider"), ctx => ctx.AddService<IShared<A>>(impl));
        Plugin consumer = loader.RegisterPlugin(
            Info("consumer", dependency: "provider"),
            ctx => ctx.AddService<IConsumer<A>>(holder));

        Assert.True(loader.EnableAsync());

        Assert.Same(impl, Service<IShared<A>>.TryResolve());
        Assert.Same(impl, holder.Shared);

        Assert.Throws<InvalidOperationException>(() => loader.DisableAsync(provider));

        Assert.True(loader.DisableAsync());
        Assert.Null(Service<IShared<A>>.TryResolve());
        Assert.Null(Service<IConsumer<A>>.TryResolve());
        Assert.Equal(PluginState.Disabled, loader.GetState(consumer));
        Assert.Equal(PluginState.Disabled, loader.GetState(provider));
    }

    [Fact]
    public async Task Enable_FailedInjection_ReturnsFalse_RollsBack()
    {
        var hub = new EventHub();
        var loader = CreateLoader(hub);
        var listener = new CountListener<E2>();
        Plugin plugin = loader.RegisterPlugin(Info("broken"), ctx => ctx
            .AddService<IOther<B>>(new OtherImpl<B>())
            .AddService<IConsumer<C>>(new ConsumerHolder<C>()) // IShared<C> 缺失 → 注入必炸
            .AddEventHandler(listener));

        Assert.False(loader.EnableAsync(plugin));
        Assert.Equal(PluginState.Failed, loader.GetState(plugin));

        Assert.Null(Service<IOther<B>>.TryResolve());
        Assert.Null(Service<IConsumer<C>>.TryResolve());
        await hub.Publish(new E2());
        Assert.Equal(0, listener.Count);
    }

    [Fact]
    public void RegisterPlugin_DuplicateName_Fails()
    {
        var loader = CreateLoader(new EventHub());
        loader.RegisterPlugin(Info("dup"), _ => { });

        Assert.Throws<InvalidPluginException>(() => loader.RegisterPlugin(Info("dup"), _ => { }));
    }

    [Fact]
    public void Load_FromAssemblyPath_ResolvesManifestAndStaticBuild()
    {
        var hub = new EventHub();
        var loader = CreateLoader(hub);
        string selfPath = typeof(SelfPluginEntry).Assembly.Location;

        Plugin plugin = loader.Load(selfPath);

        Assert.Equal("self", plugin.Name);
        Assert.Equal(PluginState.Loaded, loader.GetState(plugin));
        Assert.True(loader.EnableAsync(plugin));
        Assert.Same(plugin, Service<ISelfServiceMarker>.CurrentRegistration?.Source);
        Assert.True(loader.DisableAsync(plugin));
        Assert.Null(Service<ISelfServiceMarker>.TryResolve());
    }

    private PluginLoader CreateLoader(EventHub hub)
        => new(PluginsRoot, hub, NullLoggerFactory.Instance);

    private string PluginsRoot => Path.Combine(_tempRoot, "Plugins");

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

    private sealed record class E1 : Event<E1>;

    private sealed record class E2 : Event<E2>;

    private sealed class CountListener<TEvent> : IEventHandler<TEvent>
        where TEvent : Event<TEvent>
    {
        public int Count;

        public Task OnInvoke(TEvent _)
        {
            Count++;
            return Task.CompletedTask;
        }
    }
}
