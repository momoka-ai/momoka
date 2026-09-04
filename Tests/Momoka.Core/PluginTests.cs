using Momoka.Core.Commands;
using Momoka.Core.Events;
using Momoka.Core.Plugins;
using Momoka.Core.Services;
using Xunit;
using System;
using System.Threading.Tasks;

namespace Momoka.Core.Tests;

/// <summary>
/// Plugin 声明面：身份 / 服务描述符登记（AddSingleton 共享、AddTransient 每次新建，ValueGetter 按
/// 生命周期封装）／指令（记录 Command.Source）／事件监听器（幂等、非法签名忽略）。
/// </summary>
public sealed class PluginTests
{
    private interface IAlphaApi
    {
    }

    private interface IBetaApi
    {
    }

    private sealed class AlphaApi : IAlphaApi
    {
    }

    private sealed class BetaApi : IBetaApi
    {
    }

    [Fact]
    public void Plugin_ExposesIdentity()
    {
        var plugin = CreatePlugin("home");

        Assert.Equal("home", plugin.Name);
        Assert.Equal("1.0.0", plugin.Version);
        Assert.Same(plugin.Info, plugin.Info);
    }

    [Fact]
    public void AddSingleton_Instance_GetterAlwaysReturnsSame()
    {
        var plugin = CreatePlugin("alpha");
        var provider = new AlphaApi();

        plugin.AddSingleton<IAlphaApi>(provider);

        var service = Assert.Single(plugin.Services);
        Assert.Equal(ServiceLifecycle.Singleton, service.Lifecycle);
        Assert.Equal(typeof(IAlphaApi), service.SourceType);
        Assert.Same(plugin, service.Plugin);
        Assert.Same(provider, service.Value());
        Assert.Same(provider, service.Value());
    }

    [Fact]
    public void AddSingleton_TypePair_ReusesOneInstance()
    {
        var plugin = CreatePlugin("beta");

        plugin.AddSingleton<IBetaApi, BetaApi>();

        var service = Assert.Single(plugin.Services);
        Assert.Equal(typeof(BetaApi), service.TargetType);
        Assert.Same(service.Value(), service.Value());
    }

    [Fact]
    public void AddTransient_TypePair_CreatesNewPerGetterCall()
    {
        var plugin = CreatePlugin("gamma");

        plugin.AddTransient<IBetaApi, BetaApi>();

        var service = Assert.Single(plugin.Services);
        Assert.Equal(ServiceLifecycle.Transient, service.Lifecycle);
        Assert.NotSame(service.Value(), service.Value());
    }

    [Fact]
    public void AddCommand_RecordsSource()
    {
        var plugin = CreatePlugin("cmd");
        var command = new TestCommand("test");

        plugin.AddCommand(command);

        Assert.Same(plugin, command.Source);
        Assert.Equal(new Command[] { command }, plugin.Commands);
        Assert.Empty(plugin.Services);
    }

    [Fact]
    public void AddEventHandler_AssemblesAnnotatedMethods_OwnerIdempotent()
    {
        var plugin = CreatePlugin("events");
        var listener = new TestListener();

        plugin.AddEventHandler(listener);
        plugin.AddEventHandler(listener); // 同一监听器重复声明 → no-op

        var handler = Assert.Single(plugin.EventHandlers);
        Assert.Same(listener, handler.Owner);
        Assert.Equal(typeof(MessageEvent), handler.EventType);
    }

    [Fact]
    public void AddEventHandler_InvalidOrUndeclaredSignatures_AreIgnored()
    {
        var plugin = CreatePlugin("bad");
        var listener = new MixedListener();

        plugin.AddEventHandler(listener);

        var handler = Assert.Single(plugin.EventHandlers);
        Assert.Equal(typeof(MessageEvent), handler.EventType);
    }

    private static Plugin CreatePlugin(string name) => new(CreateInfo(name));

    private static PluginInfo CreateInfo(string name)
        => new()
        {
            Name = name,
            Version = "1.0.0",
            Main = $"{name}.Entry, Fake",
        };

    private sealed class TestCommand : Command
    {
        public TestCommand(string name)
            : base(name)
        {
        }
    }

    private sealed record class MessageEvent : Event;

    private sealed class TestListener : IEventHandler
    {
        [EventHandler]
        public void OnMessage(MessageEvent e)
        {
        }
    }

    /// <summary>带非法/未命中签名的监听器：仅合法方法被装配，其余被过滤。</summary>
    private sealed class MixedListener : IEventHandler
    {
        [EventHandler]
        public Task TaskReturn(MessageEvent _) => Task.CompletedTask;

        [EventHandler]
        public void NoParam()
        {
        }

        [EventHandler]
        public void IntParam(int value)
        {
        }

        [EventHandler]
        public void Valid(MessageEvent e)
        {
        }
    }
}
