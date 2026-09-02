using Microsoft.Extensions.Logging.Abstractions;
using Momoka.Core.Commands;
using Momoka.Core.Events;
using Momoka.Core.Plugins;
using Momoka.Core.Services;
using Xunit;

namespace Momoka.Core.Tests;

/// <summary>Plugin 声明面：身份与环境派生 / 服务写入 Service&lt;T&gt;（先到先得、覆盖、来源）/
/// 指令与事件监听器声明 / 实例登记 / 空参守卫。</summary>
public sealed class PluginTests : IDisposable
{
    private readonly string _tempRoot;

    public PluginTests()
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

    private interface IAlphaApi
    {
    }

    private interface IBetaApi
    {
    }

    private interface IGammaApi
    {
    }

    private sealed class AlphaApi : IAlphaApi
    {
    }

    private sealed class BetaApi : IBetaApi
    {
    }

    private sealed class GammaApi : IGammaApi
    {
    }

    private sealed class GammaApi2 : IGammaApi
    {
    }

    [Fact]
    public void Plugin_ExposesIdentityAndLogger()
    {
        var plugin = CreatePlugin("home");

        Assert.Equal("home", plugin.Name);
        Assert.Equal("1.0.0", plugin.Version);
        Assert.Same(plugin.Info, plugin.Info);
        Assert.NotNull(plugin.Logger);
    }

    [Fact]
    public void GetPluginFolder_CreatesDirectoryOnFirstAccess()
    {
        var plugin = CreatePlugin("folder");
        var folder = new DirectoryInfo(Path.Combine(PluginsRoot, "folder"));

        Assert.False(folder.Exists);
        var created = plugin.GetPluginFolder();
        folder.Refresh();
        Assert.True(folder.Exists);
        Assert.Equal(folder.FullName, created.FullName);
    }

    [Fact]
    public void GetPluginConfig_CreatesFileOnFirstAccess()
    {
        var plugin = CreatePlugin("config");
        var config = new FileInfo(Path.Combine(PluginsRoot, "config", "config.toml"));

        Assert.False(config.Exists);
        var created = plugin.GetPluginConfig();
        config.Refresh();
        Assert.True(config.Exists);
        Assert.Equal(config.FullName, created.FullName);
    }

    [Fact]
    public void AddService_RegistersProvider_WithPluginAsSource()
    {
        var plugin = CreatePlugin("alpha");
        var provider = new AlphaApi();

        Assert.Same(plugin, plugin.AddService<IAlphaApi>(provider));
        Assert.Same(provider, Service<IAlphaApi>.TryResolve());

        var registration = Assert.Single(Service<IAlphaApi>.Registrations);
        Assert.Same(plugin, registration.Source);
        var declaration = Assert.Single(plugin.ServiceProviders);
        Assert.Equal(typeof(IAlphaApi), declaration.ServiceType);
        Assert.Same(provider, declaration.Provider);
        Service<IAlphaApi>.Remove(plugin);
    }

    [Fact]
    public void AddService_SecondPlugin_BecomesFallback()
    {
        var first = CreatePlugin("first");
        var second = CreatePlugin("second");
        var providerA = new BetaApi();
        var providerB = new BetaApi();

        first.AddService<IBetaApi>(providerA);
        second.AddService<IBetaApi>(providerB);

        Assert.Same(providerA, Service<IBetaApi>.Current);
        Assert.Equal(new[] { providerA, providerB }, Service<IBetaApi>.All);
        Service<IBetaApi>.Remove(first);
        Service<IBetaApi>.Remove(second);
    }

    [Fact]
    public void AddService_Overwrite_ReplacesCurrentProvider()
    {
        var first = CreatePlugin("first");
        var second = CreatePlugin("second");
        var providerA = new GammaApi();
        var providerB = new GammaApi2();

        first.AddService<IGammaApi>(providerA);
        second.AddService<IGammaApi>(providerB, overwrite: true);

        Assert.Same(providerB, Service<IGammaApi>.Current);
        Service<IGammaApi>.Remove(first);
        Service<IGammaApi>.Remove(second);
    }

    [Fact]
    public void AddCommandAndEventHandler_DeclareSeparately_ServiceProvidersStayEmpty()
    {
        var plugin = CreatePlugin("decl");
        var command = new TestCommand("test");
        var listener = new TestListener();

        Plugin returned = plugin
            .AddCommand(command)
            .AddEventHandler(listener);

        Assert.Same(plugin, returned);
        Assert.Equal(new Command[] { command }, plugin.Commands);
        Assert.Equal(new object[] { listener }, plugin.EventHandlers);
        Assert.Empty(plugin.ServiceProviders);
    }

    [Fact]
    public void NullArguments_Throw()
    {
        var plugin = CreatePlugin("guards");

        Assert.Throws<ArgumentNullException>(() => new Plugin(null!, PluginsRoot));
        Assert.Throws<ArgumentException>(() => new Plugin(CreateInfo("guards"), "  "));
        Assert.Throws<ArgumentNullException>(() => plugin.AddService<IAlphaApi>(null!));
        Assert.Throws<ArgumentNullException>(() => plugin.AddCommand(null!));
        Assert.Throws<ArgumentNullException>(() => plugin.AddEventHandler(null!));
    }

    private string PluginsRoot => Path.Combine(_tempRoot, "Plugins");

    private Plugin CreatePlugin(string name)
        => new(CreateInfo(name), PluginsRoot, NullLoggerFactory.Instance);

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

    private sealed class TestListener 
    {
    }
}
