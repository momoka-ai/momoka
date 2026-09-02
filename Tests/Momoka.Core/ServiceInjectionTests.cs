using Microsoft.Extensions.Logging.Abstractions;
using Momoka.Core.Commands;
using Momoka.Core.Events;
using Momoka.Core.Plugins;
using Momoka.Core.Services;
using Xunit;

namespace Momoka.Core.Tests;

/// <summary>[ServiceInjection] 注入 pass：仅服务提供者参与 / 可空性硬失败开关 /
/// 使用图边记录 / Core 管理对象（监听器/指令）不被注入。</summary>
public sealed class ServiceInjectionTests : IDisposable
{
    private readonly string _tempRoot;

    public ServiceInjectionTests()
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

    private interface IService<T>
    {
    }

    private interface IConsumer<T>
    {
    }

    private sealed class ServiceImpl<T> : IService<T>
    {
    }

    private sealed class RequiredConsumer<T> : IConsumer<T>
    {
        [ServiceInjection]
        public IService<T> Service { get; set; } = null!;
    }

    private sealed class OptionalConsumer<T> : IConsumer<T>
    {
        [ServiceInjection]
        public IService<T>? Service { get; set; }
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
    public void Inject_RequiredProperty_FillsAndRecordsUsageEdge()
    {
        var provider = CreatePlugin("provider");
        var consumer = CreatePlugin("consumer");
        var graph = new ServiceUsageGraph();
        var impl = new ServiceImpl<A>();
        var holder = new RequiredConsumer<A>();
        provider.AddService<IService<A>>(impl);
        consumer.AddService<IConsumer<A>>(holder);

        ServiceInjector.Inject(consumer, graph);

        Assert.Same(impl, holder.Service);
        Assert.Equal(new[] { consumer }, graph.GetUsers(provider));
        Service<IService<A>>.Remove(provider);
        Service<IConsumer<A>>.Remove(consumer);
    }

    [Fact]
    public void Inject_MissingRequiredService_Throws()
    {
        var consumer = CreatePlugin("consumer");
        var holder = new RequiredConsumer<B>();
        consumer.AddService<IConsumer<B>>(holder);

        var ex = Assert.Throws<InvalidOperationException>(() => ServiceInjector.Inject(consumer));

        Assert.Contains(nameof(RequiredConsumer<B>), ex.Message);
        Service<IConsumer<B>>.Remove(consumer);
    }

    [Fact]
    public void Inject_MissingOptionalService_LeavesNull()
    {
        var consumer = CreatePlugin("consumer");
        var holder = new OptionalConsumer<C>();
        consumer.AddService<IConsumer<C>>(holder);

        ServiceInjector.Inject(consumer);

        Assert.Null(holder.Service);
        Service<IConsumer<C>>.Remove(consumer);
    }

    [Fact]
    public void Inject_IgnoresCoreManagedObjects()
    {
        var plugin = CreatePlugin("plugin");
        var listener = new InjectedListener();
        plugin.AddEventHandler(listener);
        plugin.AddCommand(new InjectedCommand());

        ServiceInjector.Inject(plugin);

        Assert.Null(listener.Service);
        Assert.Null(((InjectedCommand)plugin.Commands[0]).Service);
    }

    private Plugin CreatePlugin(string name)
        => new(CreateInfo(name), Path.Combine(_tempRoot, "Plugins"), NullLoggerFactory.Instance);

    private static PluginInfo CreateInfo(string name)
        => new()
        {
            Name = name,
            Version = "1.0.0",
            Main = $"{name}.Entry, Fake",
        };

    /// <summary>Core 管理对象即使携带 [ServiceInjection] 也不参与注入。</summary>
    private sealed class InjectedListener : IEventHandler<InjectedEvent>
    {
        [ServiceInjection]
        public IService<C>? Service { get; set; }

        public Task OnInvoke(InjectedEvent _) => Task.CompletedTask;
    }

    private sealed record class InjectedEvent : Event<InjectedEvent>;

    private sealed class InjectedCommand : Command
    {
        public InjectedCommand()
            : base("injected")
        {
        }

        [ServiceInjection]
        public IService<C>? Service { get; set; }
    }
}
