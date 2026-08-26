using Xunit;
using System.Reflection;
using System.Runtime.Loader;
using Microsoft.Extensions.Logging.Abstractions;
using Momoka.Core.Events;
using Momoka.Core.Plugins;

namespace Momoka.Core.Tests;

/// <summary>
/// 插件加载器集成测试：真实插件 DLL 拷入独立临时目录后经宿主加载。
/// 覆盖 Load 记录 / 非法插件 fail-fast / 单插件与批量启停 / 依赖排序 / 跨插件程序集解析 /
/// 失败状态 / 回滚 / 查询与静态内省原语。
/// </summary>
public sealed class PluginLoaderTests : IDisposable
{
    private static readonly string LifecyclePath =
        Path.Combine(AppContext.BaseDirectory, "plugin-lifecycle.log");

    private readonly string _tempRoot;
    private readonly string _pluginsDir;

    public PluginLoaderTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), "momoka-core-tests", Guid.NewGuid().ToString("N"));
        _pluginsDir = Path.Combine(_tempRoot, "Plugins");
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
    public void EmptyPluginDirectory_NoPlugins()
    {
        Directory.CreateDirectory(_pluginsDir);
        using var loader = CreateLoader();

        Assert.Empty(PluginLoader.GetPluginFiles(_pluginsDir));
        Assert.Empty(loader.Plugins);
    }

    [Fact]
    public void Load_RecordsAssemblyAndInstance()
    {
        CopyPlugins("alpha");
        using var loader = CreateLoader();

        var plugin = loader.Load(AlphaPath());

        Assert.Equal("alpha", plugin.Name);
        Assert.Equal("1.0.0", plugin.Version);
        Assert.Equal(PluginState.Loaded, plugin.State);
        Assert.Single(loader.PluginAssemblies);
        Assert.Single(loader.Plugins);
        Assert.Same(plugin, loader.Plugins[0]);
        Assert.Equal(AlphaPath(), loader.PluginAssemblies[0].Path);
    }

    [Fact]
    public void Load_NonPluginDll_Throws()
    {
        var dependencyDir = Directory.CreateDirectory(Path.Combine(_pluginsDir, "deps"));
        string tomlynPath = Path.Combine(dependencyDir.FullName, "Tomlyn.dll");
        File.Copy(typeof(Tomlyn.TomlSerializer).Assembly.Location, tomlynPath);
        using var loader = CreateLoader();

        var ex = Assert.Throws<InvalidPluginException>(() => loader.Load(tomlynPath));
        Assert.Contains("missing plugin.toml", ex.Message);
    }

    [Fact]
    public void Load_BadMain_Throws()
    {
        CopyPlugins("bad");
        using var loader = CreateLoader();

        Assert.Throws<InvalidPluginException>(() => loader.Load(BadPath()));
    }

    [Fact]
    public void Load_NotPluginSubclass_Throws()
    {
        CopyPlugins("plain");
        using var loader = CreateLoader();

        var ex = Assert.Throws<InvalidPluginException>(() => loader.Load(PlainPath()));
        Assert.Contains("Plugin", ex.Message);
    }

    [Fact]
    public void Load_DuplicateName_Throws()
    {
        CopyPlugins("alpha");
        using var loader = CreateLoader();

        loader.Load(AlphaPath());
        Assert.Throws<InvalidPluginException>(() => loader.Load(AlphaPath()));
    }

    [Fact]
    public void EnableDisable_SinglePlugin_Lifecycle()
    {
        ClearLifecycle();
        CopyPlugins("alpha");
        using var loader = CreateLoader();
        var plugin = loader.Load(AlphaPath());

        Assert.True(loader.EnableAsync(plugin));
        Assert.Equal(PluginState.Enabled, plugin.State);
        Assert.Equal(new[] { "alpha:enable" }, ReadLifecycle());

        Assert.True(loader.DisableAsync(plugin));
        Assert.Equal(PluginState.Disabled, plugin.State);
        Assert.Equal(new[] { "alpha:enable", "alpha:disable" }, ReadLifecycle());
    }

    [Fact]
    public void Enable_AlreadyEnabled_ReturnsFalse()
    {
        CopyPlugins("alpha");
        using var loader = CreateLoader();
        var plugin = loader.Load(AlphaPath());

        Assert.True(loader.EnableAsync(plugin));
        Assert.False(loader.EnableAsync(plugin));
    }

    [Fact]
    public void Enable_NotLoaded_ReturnsFalse()
    {
        using var loader = CreateLoader();

        Assert.False(loader.EnableAsync(new UnloadedPlugin()));
    }

    [Fact]
    public void Enable_Failure_MarksFailed()
    {
        CopyPlugins("explode");
        using var loader = CreateLoader();
        var plugin = loader.Load(ExplodePath());
        SetStaticBool(ExplodePath(), "ThrowOnEnable", true);

        Assert.False(loader.EnableAsync(plugin));
        Assert.Equal(PluginState.Failed, plugin.State);
    }

    [Fact]
    public void Disable_Failure_MarksFailed()
    {
        CopyPlugins("explode");
        using var loader = CreateLoader();
        var plugin = loader.Load(ExplodePath());

        Assert.True(loader.EnableAsync(plugin));
        SetStaticBool(ExplodePath(), "ThrowOnDisable", true);

        Assert.False(loader.DisableAsync(plugin));
        Assert.Equal(PluginState.Failed, plugin.State);
    }

    [Fact]
    public async Task EnableAll_OrdersDependenciesAndResolvesCrossAssemblyServices()
    {
        ClearLifecycle();
        CopyPlugins("alpha", "beta");
        using var loader = CreateLoader();
        loader.Load(AlphaPath());
        loader.Load(BetaPath());

        Assert.True(await loader.EnableAsync());

        var betaAssembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(BetaPath());
        var betaType = betaAssembly.GetType("Momoka.Core.Tests.Plugins.Beta.BetaPlugin", throwOnError: true)!;
        Assert.Equal("hello from alpha", betaType.GetProperty("ResolvedGreeting")!.GetValue(null));
        Assert.Equal(new[] { "alpha:enable", "beta:enable" }, ReadLifecycle());
    }

    [Fact]
    public async Task DisableAll_DisablesInReverseGraphOrder()
    {
        ClearLifecycle();
        CopyPlugins("alpha", "beta");
        using var loader = CreateLoader();
        loader.Load(AlphaPath());
        loader.Load(BetaPath());
        await loader.EnableAsync();

        Assert.True(await loader.DisableAsync());

        Assert.Equal(
            new[] { "alpha:enable", "beta:enable", "beta:disable", "alpha:disable" },
            ReadLifecycle());
    }

    [Fact]
    public async Task EnableAll_Failure_RollsBackEnabledPlugins()
    {
        CopyPlugins("alpha", "explode");
        using var loader = CreateLoader();
        var alpha = loader.Load(AlphaPath());
        var explode = loader.Load(ExplodePath());
        SetStaticBool(ExplodePath(), "ThrowOnEnable", true);

        Assert.False(await loader.EnableAsync());
        Assert.Equal(PluginState.Disabled, alpha.State);  // 回滚
        Assert.Equal(PluginState.Failed, explode.State);
    }

    [Fact]
    public void GetPlugin_FindsByName()
    {
        CopyPlugins("alpha");
        using var loader = CreateLoader();
        var plugin = loader.Load(AlphaPath());

        Assert.Same(plugin, loader.GetPlugin("alpha"));
        Assert.Null(loader.GetPlugin("missing"));
    }

    [Fact]
    public void GetPluginDependencies_ReturnsForwardDependencies()
    {
        CopyPlugins("alpha", "beta");
        using var loader = CreateLoader();
        var alpha = loader.Load(AlphaPath());
        var beta = loader.Load(BetaPath());

        var dependencies = loader.GetPluginDependencies(beta);

        Assert.Single(dependencies);
        Assert.Same(alpha, dependencies.Single());
        Assert.Empty(loader.GetPluginDependencies(alpha));
    }

    [Fact]
    public void GetPluginDependents_ReturnsReverseDependencies()
    {
        CopyPlugins("alpha", "beta");
        using var loader = CreateLoader();
        var alpha = loader.Load(AlphaPath());
        loader.Load(BetaPath());

        var dependents = loader.GetPluginDependents(alpha);

        Assert.Single(dependents);
        Assert.Equal("beta", dependents.Single().Name);
    }

    [Fact]
    public void GetPluginInfo_ReadsManifest()
    {
        CopyPlugins("alpha");

        var info = PluginLoader.GetPluginInfo(AlphaPath());

        Assert.NotNull(info);
        Assert.Equal("alpha", info.Name);
        Assert.Equal("1.0.0", info.Version);
    }

    [Fact]
    public void GetPluginInfo_NonPluginAssembly_ReturnsNull()
    {
        var dependencyDir = Directory.CreateDirectory(Path.Combine(_pluginsDir, "deps"));
        string tomlynPath = Path.Combine(dependencyDir.FullName, "Tomlyn.dll");
        File.Copy(typeof(Tomlyn.TomlSerializer).Assembly.Location, tomlynPath);

        Assert.Null(PluginLoader.GetPluginInfo(tomlynPath));
    }

    [Fact]
    public void GetPluginResource_ReturnsStream()
    {
        CopyPlugins("alpha");
        string resourceName = AssemblyLoadContext.Default.LoadFromAssemblyPath(AlphaPath()).GetManifestResourceNames()
            .Single(n => n.EndsWith(".plugin.toml", StringComparison.OrdinalIgnoreCase));

        using var stream = PluginLoader.GetPluginResource(AlphaPath(), resourceName);

        Assert.NotNull(stream);
        Assert.True(stream.Length > 0);
    }

    [Fact]
    public void GetPluginFiles_ListsCandidateDlls()
    {
        CopyPlugins("alpha", "beta");

        var files = PluginLoader.GetPluginFiles(_pluginsDir);

        Assert.Contains(AlphaPath(), files);
        Assert.Contains(BetaPath(), files);
    }

    [Fact]
    public void GetPluginMainType_ResolvesConcretePluginSubclass()
    {
        CopyPlugins("alpha");
        var info = PluginLoader.GetPluginInfo(AlphaPath())!;
        var assembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(AlphaPath());

        var type = PluginLoader.GetPluginMainType(info, assembly);

        Assert.NotNull(type);
        Assert.Equal("Momoka.Core.Tests.Plugins.Alpha.AlphaPlugin", type.FullName);
        Assert.True(typeof(Plugin).IsAssignableFrom(type));
    }

    private PluginLoader CreateLoader()
    {
        var service = new PluginService(
            new ServiceRegistry(), new EventHub(), NullLoggerFactory.Instance, _tempRoot);
        return new PluginLoader(service);
    }

    private void CopyPlugins(params string[] pluginIds)
    {
        foreach (var id in pluginIds)
        {
            var source = Path.Combine(AppContext.BaseDirectory, "Plugins", id);
            var target = Path.Combine(_pluginsDir, id);
            Directory.CreateDirectory(target);
            foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
            {
                var relative = Path.GetRelativePath(source, file);
                var destination = Path.Combine(target, relative);
                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                File.Copy(file, destination, overwrite: true);
            }
        }
    }

    private string AlphaPath() => Path.Combine(_pluginsDir, "alpha", "PluginAlpha.dll");

    private string BetaPath() => Path.Combine(_pluginsDir, "beta", "PluginBeta.dll");

    private string BadPath() => Path.Combine(_pluginsDir, "bad", "PluginBad.dll");

    private string PlainPath() => Path.Combine(_pluginsDir, "plain", "PluginPlain.dll");

    private string ExplodePath() => Path.Combine(_pluginsDir, "explode", "PluginExplode.dll");

    private static void SetStaticBool(string assemblyPath, string propertyName, bool value)
    {
        var assembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(assemblyPath);
        var type = assembly.GetType("Momoka.Core.Tests.Plugins.Explode.ExplodePlugin", throwOnError: true)!;
        type.GetProperty(propertyName, BindingFlags.Public | BindingFlags.Static)!.SetValue(null, value);
    }

    private static void ClearLifecycle()
    {
        if (File.Exists(LifecyclePath))
        {
            File.Delete(LifecyclePath);
        }
    }

    private static List<string> ReadLifecycle() =>
        File.Exists(LifecyclePath)
            ? File.ReadAllLines(LifecyclePath).Where(l => !string.IsNullOrWhiteSpace(l)).ToList()
            : new List<string>();

    /// <summary>未加载的测试插件（验证 EnableAsync 对非本加载器实例返回 false）。</summary>
    private sealed class UnloadedPlugin : Plugin
    {
    }
}
