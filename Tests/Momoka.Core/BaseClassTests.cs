using Xunit;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Momoka.Core.Events;
using Momoka.Core.Plugins;

namespace Momoka.Core.Tests;

/// <summary>CorePlugin 基类：宿主注入直通/专属能力派生/守卫/目录自动创建。</summary>
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
        plugin.Load();

        Assert.True(plugin.Loaded);
        Assert.Same(registry, plugin.ServicesPublic);
        Assert.Same(hub, plugin.EventsPublic);
        Assert.NotNull(plugin.LoggerPublic);
        Assert.Equal("test", plugin.Name);
        Assert.Equal("1.0.0", plugin.Version);
    }

    [Fact]
    public void Load_Twice_Throws()
    {
        var plugin = new TestPlugin();
        plugin.InjectHost(
            CreateInfo("test"),
            CreateService(new ServiceRegistry(), new EventHub()));
        plugin.Load();

        var ex = Assert.Throws<InvalidOperationException>(() => plugin.Load());
        Assert.Contains("already been loaded", ex.Message);
    }

    [Fact]
    public void GetPluginFolder_CreatesDirectoryOnFirstAccess()
    {
        var plugin = InjectedPlugin(out var folder, out _);
        plugin.Load();

        Assert.False(folder.Exists);
        _ = plugin.Folder;
        folder.Refresh();
        Assert.True(folder.Exists);
    }

    [Fact]
    public void GetPluginConfig_CreatesFileOnFirstAccess()
    {
        var plugin = InjectedPlugin(out _, out var config);
        plugin.Load();

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
        plugin.Load();
        var hub = plugin.EventsPublic;

        var calls = 0;
        using var token = hub.Subscribe<int>(_ =>
        {
            calls++;
            return Task.CompletedTask;
        });

        await hub.PublishAsync(1);
        Assert.Equal(1, calls);

        token.Dispose();
        await hub.PublishAsync(2);
        Assert.Equal(1, calls);
    }

    [Fact]
    public void OnLoad_IsInvokedOnLoad()
    {
        var plugin = InjectedPlugin(out _, out _);

        Assert.False(plugin.Loaded);
        plugin.Load();
        Assert.True(plugin.Loaded);
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

    /// <summary>内联测试插件：暴露 CorePlugin 的 protected 成员供测试访问。</summary>
    public sealed class TestPlugin : CorePlugin
    {
        public ServiceRegistry ServicesPublic => Plugin.Services;

        public EventHub EventsPublic => Plugin.Events;

        public ILogger LoggerPublic => Logger;

        public DirectoryInfo Folder => GetPluginFolder();

        public FileInfo Config => GetPluginConfig();

        public bool Loaded { get; private set; }

        protected override void OnLoad()
        {
            Loaded = true;
        }

        public override Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public override Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
