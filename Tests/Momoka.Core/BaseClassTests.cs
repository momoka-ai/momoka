using Xunit;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Momoka.Core.Events;
using Momoka.Core.Plugins;

namespace Momoka.Core.Tests;

/// <summary>Plugin 基类：宿主注入直通/专属能力派生/守卫/目录自动创建/生命周期钩子。</summary>
public sealed class BaseClassTests : IDisposable
{
    private readonly string _tempRoot;

    public BaseClassTests()
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

    [Fact]
    public void AccessHostCapabilities_BeforeInjection_Throws()
    {
        var plugin = new TestPlugin();

        Assert.Throws<InvalidOperationException>(() => _ = plugin.ServicesPublic);
        Assert.Throws<InvalidOperationException>(() => _ = plugin.EventsPublic);
        Assert.Throws<InvalidOperationException>(() => _ = plugin.LoggerPublic);
        Assert.Throws<InvalidOperationException>(() => _ = plugin.Folder);
        Assert.Throws<InvalidOperationException>(() => _ = plugin.Config);
    }

    [Fact]
    public void InjectHost_ExposesCapabilities()
    {
        var registry = new ServiceRegistry();
        var hub = new EventHub();

        var plugin = new TestPlugin();
        plugin.InjectHost(CreateInfo("test"), CreateService(registry, hub));

        Assert.Same(registry, plugin.ServicesPublic);
        Assert.Same(hub, plugin.EventsPublic);
        Assert.NotNull(plugin.LoggerPublic);
        Assert.Equal("test", plugin.Name);
        Assert.Equal("1.0.0", plugin.Version);
        Assert.Equal(PluginState.Loaded, plugin.State);
    }

    [Fact]
    public void GetPluginFolder_CreatesDirectoryOnFirstAccess()
    {
        var plugin = InjectedPlugin(out var folder, out _);

        Assert.False(folder.Exists);
        _ = plugin.Folder;
        folder.Refresh();
        Assert.True(folder.Exists);
    }

    [Fact]
    public void GetPluginConfig_CreatesFileOnFirstAccess()
    {
        var plugin = InjectedPlugin(out _, out var config);

        Assert.False(config.Exists);
        _ = plugin.Config;
        config.Refresh();
        Assert.True(config.Exists);
        config.Directory!.Refresh();
        Assert.True(config.Directory.Exists);
    }

    [Fact]
    public async Task PluginService_ExposesEventHubForSubscription()
    {
        var plugin = InjectedPlugin(out _, out _);
        var hub = plugin.EventsPublic;

        var listener = new IntListener();
        hub.AddSubscribers(listener);
        await hub.InvokeAsync(1);
        Assert.Equal(1, listener.Count);

        hub.RemoveSubscribers(listener);
        await hub.InvokeAsync(2);
        Assert.Equal(1, listener.Count);
    }

    [Fact]
    public void OnEnable_IsInvoked()
    {
        var plugin = InjectedPlugin(out _, out _);

        Assert.False(plugin.Enabled);
        plugin.OnEnable();
        Assert.True(plugin.Enabled);
    }

    [Fact]
    public void GetPluginResource_ReadsEmbeddedResource()
    {
        var plugin = InjectedPlugin(out _, out _);

        using Stream stream = plugin.Resource("Momoka.Core.Tests.Resources.greeting.txt")!;
        using var reader = new StreamReader(stream);
        Assert.Equal("hello resource", reader.ReadToEnd());
    }

    [Fact]
    public void GetPluginResource_MissingResource_ReturnsNull()
    {
        var plugin = InjectedPlugin(out _, out _);

        Assert.Null(plugin.Resource("no.such.resource"));
    }

    private TestPlugin InjectedPlugin(out DirectoryInfo folder, out FileInfo config)
    {
        folder = new DirectoryInfo(Path.Combine(_tempRoot, "Plugins", "test"));
        config = new FileInfo(Path.Combine(_tempRoot, "Plugins", "test", "config.toml"));
        var plugin = new TestPlugin();
        plugin.InjectHost(
            CreateInfo("test"),
            CreateService(new ServiceRegistry(), new EventHub()));
        return plugin;
    }

    private static PluginInfo CreateInfo(string name) =>
        new()
        {
            Name = name,
            Version = "1.0.0",
            Main = $"{name}.Entry, Fake",
        };

    private PluginService CreateService(ServiceRegistry registry, EventHub hub)
        => new(registry, hub, NullLoggerFactory.Instance, _tempRoot);

    /// <summary>事件监听测试载体（Subscribers 实现）。</summary>
    public sealed class IntListener : Subscribers
    {
        public int Count;

        [Subscribe(typeof(int))]
        public Task On(int value)
        {
            Count++;
            return Task.CompletedTask;
        }
    }

    /// <summary>内联测试插件：暴露 Plugin 的 protected 成员供测试访问。</summary>
    public sealed class TestPlugin : Plugin
    {
        public ServiceRegistry ServicesPublic => Host.Services;

        public EventHub EventsPublic => Host.Events;

        public ILogger LoggerPublic => Logger;

        public DirectoryInfo Folder => GetPluginFolder();

        public FileInfo Config => GetPluginConfig();

        public Stream? Resource(string path) => GetPluginResource(path);

        public bool Enabled { get; private set; }

        public override void OnEnable()
        {
            Enabled = true;
        }
    }
}
