using Xunit;
using System.Reflection;
using Momoka.Core.Events;
using Momoka.Core.Plugins;
using Momoka.Core.Registry;

namespace Momoka.Core.Tests;

/// <summary>
/// 插件加载器集成测试：真实插件 DLL 拷入独立临时目录后经宿主加载。
/// 覆盖扫描/依赖排序/禁用/入口校验/回滚/逆序停止/跨插件程序集解析/回填。
/// </summary>
public sealed class PluginLoaderTests : IDisposable
{
    private static readonly string LifecyclePath =
        Path.Combine(AppContext.BaseDirectory, "plugin-lifecycle.log");

    private readonly string _tempRoot;
    private readonly string _pluginsDir;
    private readonly string _configDir;
    private readonly string _dataDir;

    public PluginLoaderTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), "momoka-core-tests", Guid.NewGuid().ToString("N"));
        _pluginsDir = Path.Combine(_tempRoot, "Plugins");
        _configDir = Path.Combine(_tempRoot, "Config");
        _dataDir = Path.Combine(_tempRoot, "Data");
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
    public async Task EmptyPluginDirectory_StartsWithNoPlugins()
    {
        using var loader = CreateLoader();

        await loader.StartAsync();
        Assert.Empty(loader.Plugins);
        await loader.StopAsync();
    }

    [Fact]
    public async Task DependencyLibraryWithoutManifest_IsSkipped()
    {
        var dependencyDir = Directory.CreateDirectory(Path.Combine(_pluginsDir, "deps"));
        File.Copy(typeof(Tomlyn.TomlSerializer).Assembly.Location,
            Path.Combine(dependencyDir.FullName, "Tomlyn.dll"));
        using var loader = CreateLoader();

        await loader.StartAsync();
        Assert.Empty(loader.Plugins);
        await loader.StopAsync();
    }

    [Fact]
    public async Task Start_OrdersDependenciesAndResolvesCrossAssemblyServices()
    {
        ClearLifecycle();
        CopyPlugins("alpha", "beta");
        using var loader = CreateLoader();

        await loader.StartAsync();

        var betaAssembly = Assembly.LoadFrom(Path.Combine(_pluginsDir, "beta", "PluginBeta.dll"));
        var betaType = betaAssembly.GetType("Momoka.Core.Tests.Plugins.Beta.BetaPlugin", throwOnError: true)!;
        Assert.Equal("hello from alpha", betaType.GetProperty("ResolvedGreeting")!.GetValue(null));

        Assert.Equal(new[] { "alpha:start", "beta:start" }, ReadLifecycle());
        await loader.StopAsync();
    }

    [Fact]
    public async Task DisabledPlugin_ViaHostConfig_IsSkipped()
    {
        Directory.CreateDirectory(_configDir);
        File.WriteAllText(Path.Combine(_configDir, "plugins.toml"), "[alpha]\nenabled = false\n");
        CopyPlugins("alpha");
        using var loader = CreateLoader();

        await loader.StartAsync();

        Assert.Contains(loader.Plugins, p => p.Name == "alpha" && p.State == PluginState.Discovered);
        Assert.DoesNotContain(loader.Plugins, p => p.State == PluginState.Started);
        await loader.StopAsync();
    }

    [Fact]
    public async Task DependencyOnDisabledPlugin_FailsFast()
    {
        Directory.CreateDirectory(_configDir);
        File.WriteAllText(Path.Combine(_configDir, "plugins.toml"), "[alpha]\nenabled = false\n");
        CopyPlugins("alpha", "beta");
        using var loader = CreateLoader();

        var ex = await Assert.ThrowsAsync<PluginLoadException>(() => loader.StartAsync());
        Assert.Contains("disabled plugin", ex.Message);
    }

    [Fact]
    public async Task EntryTypeNotFound_FailsFast()
    {
        CopyPlugins("bad");
        using var loader = CreateLoader();

        var ex = await Assert.ThrowsAsync<PluginLoadException>(() => loader.StartAsync());
        Assert.Contains("was not found", ex.Message);
    }

    [Fact]
    public async Task EntryNotCorePlugin_FailsFast()
    {
        CopyPlugins("plain");
        using var loader = CreateLoader();

        var ex = await Assert.ThrowsAsync<PluginLoadException>(() => loader.StartAsync());
        Assert.Contains("CorePlugin", ex.Message);
    }

    [Fact]
    public async Task LoadFailure_RollsBackStartedPlugins()
    {
        CopyPlugins("alpha", "explode");
        SetStaticBool(ExplodePath(), "ThrowOnLoad", true);
        using var loader = CreateLoader();

        await Assert.ThrowsAsync<PluginLoadException>(() => loader.StartAsync());

        Assert.Contains(loader.Plugins, p => p.Name == "alpha" && p.State == PluginState.Stopped);
        Assert.Contains(loader.Plugins, p => p.Name == "explode" && p.State == PluginState.Failed);
    }

    [Fact]
    public async Task StartFailure_RollsBackStartedPlugins()
    {
        CopyPlugins("alpha", "explode");
        SetStaticBool(ExplodePath(), "ThrowOnStart", true);
        using var loader = CreateLoader();

        await Assert.ThrowsAsync<PluginLoadException>(() => loader.StartAsync());

        Assert.Contains(loader.Plugins, p => p.Name == "alpha" && p.State == PluginState.Stopped);
        Assert.Contains(loader.Plugins, p => p.Name == "explode" && p.State == PluginState.Failed);
    }

    [Fact]
    public async Task StopAsync_StopsInReverseStartOrder()
    {
        ClearLifecycle();
        CopyPlugins("alpha", "beta");
        using var loader = CreateLoader();

        await loader.StartAsync();
        await loader.StopAsync();

        Assert.Equal(
            new[] { "alpha:start", "beta:start", "beta:stop", "alpha:stop" },
            ReadLifecycle());
    }

    [Fact]
    public async Task BackfillsNameAndVersionFromManifest()
    {
        CopyPlugins("alpha", "beta");
        using var loader = CreateLoader();

        await loader.StartAsync();

        Assert.Contains(loader.Plugins, p => p.Name == "alpha" && p.Version == "1.0.0");
        Assert.Contains(loader.Plugins, p => p.Name == "beta" && p.Version == "2.0.0");
        await loader.StopAsync();
    }

    [Fact]
    public async Task StartTwice_Throws()
    {
        CopyPlugins("alpha");
        using var loader = CreateLoader();

        await loader.StartAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() => loader.StartAsync());
        await loader.StopAsync();
    }

    private PluginLoader CreateLoader()
    {
        var options = new PluginLoaderOptions(
            new DirectoryInfo(_pluginsDir),
            new DirectoryInfo(_configDir),
            new DirectoryInfo(_dataDir));
        return new PluginLoader(options, new ServiceRegistry(), new EventBus());
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

    private string ExplodePath() => Path.Combine(_pluginsDir, "explode", "PluginExplode.dll");

    private static void SetStaticBool(string assemblyPath, string propertyName, bool value)
    {
        var assembly = Assembly.LoadFrom(assemblyPath);
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
}
