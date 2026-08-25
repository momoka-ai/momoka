using Xunit;
using Momoka.Core.Plugins;

namespace Momoka.Core.Tests;

/// <summary>PluginInfo（plugin.toml 直接反序列化）与依赖图纯函数：解析/缺字段/重复名/未知依赖/依赖环/排序。</summary>
public sealed class PluginInfoTests
{
    private static PluginInfo Plugin(string name, params string[] dependsOn) =>
        new()
        {
            Name = name,
            Version = "1.0.0",
            Entry = $"{name}.Entry, Fake",
            DependsOn = dependsOn,
            Location = new DirectoryInfo("."),
        };

    private static HashSet<string> NoDisabled => new(StringComparer.Ordinal);

    [Fact]
    public void Parse_ValidManifest_ReturnsFields()
    {
        const string toml = """
            name = "home"
            version = "1.2.3"
            entry = "Momoka.Home.HomePlugin, Momoka.Home"
            dependsOn = ["ai", "sense"]
            """;

        var info = PluginInfo.Parse(toml, "plugin.toml");

        Assert.Equal("home", info.Name);
        Assert.Equal("1.2.3", info.Version);
        Assert.Equal("Momoka.Home.HomePlugin, Momoka.Home", info.Entry);
        Assert.Equal(new[] { "ai", "sense" }, info.DependsOn);
    }

    [Fact]
    public void Parse_DependsOnOptional_DefaultsToEmpty()
    {
        const string toml = """
            name = "home"
            version = "1.0.0"
            entry = "Momoka.Home.HomePlugin, Momoka.Home"
            """;

        var info = PluginInfo.Parse(toml, "plugin.toml");

        Assert.Empty(info.DependsOn);
    }

    [Fact]
    public void Parse_IgnoresNonManifestKeys()
    {
        const string toml = """
            name = "home"
            version = "1.0.0"
            entry = "Momoka.Home.HomePlugin, Momoka.Home"
            settings = { anything = 1 }
            """;

        var info = PluginInfo.Parse(toml, "plugin.toml");

        Assert.Equal("home", info.Name);
        Assert.Equal(PluginState.Discovered, info.State);
        Assert.Null(info.Location);
    }

    [Theory]
    [InlineData("name = \"x\"\nversion = \"1\"\n")]
    [InlineData("name = \"x\"\nentry = \"a.b\"\n")]
    [InlineData("version = \"1\"\nentry = \"a.b\"\n")]
    [InlineData("name = \"\"\nversion = \"1\"\nentry = \"a.b\"\n")]
    public void Parse_MissingRequiredField_Throws(string toml)
    {
        Assert.Throws<PluginLoadException>(() => PluginInfo.Parse(toml, "plugin.toml"));
    }

    [Fact]
    public void Parse_InvalidSyntax_Throws()
    {
        Assert.Throws<PluginLoadException>(() => PluginInfo.Parse("name = [", "plugin.toml"));
    }

    [Fact]
    public void Parse_WrongFieldType_Throws()
    {
        const string toml = """
            name = 42
            version = "1"
            entry = "a.b"
            """;

        Assert.Throws<PluginLoadException>(() => PluginInfo.Parse(toml, "plugin.toml"));
    }

    [Fact]
    public void Order_SortsDependenciesFirst()
    {
        var plugins = new[]
        {
            Plugin("beta", "alpha"),
            Plugin("alpha"),
            Plugin("gamma", "beta"),
        };

        var ordered = PluginDependencyGraph.Order(plugins, NoDisabled);

        Assert.Equal(new[] { "alpha", "beta", "gamma" }, ordered.Select(p => p.Name).ToArray());
    }

    [Fact]
    public void Order_DuplicateNames_Throws()
    {
        var plugins = new[] { Plugin("alpha"), Plugin("alpha") };

        var ex = Assert.Throws<PluginLoadException>(() => PluginDependencyGraph.Order(plugins, NoDisabled));
        Assert.Contains("Duplicate", ex.Message);
    }

    [Fact]
    public void Order_UnknownDependency_Throws()
    {
        var plugins = new[] { Plugin("beta", "missing") };

        var ex = Assert.Throws<PluginLoadException>(() => PluginDependencyGraph.Order(plugins, NoDisabled));
        Assert.Contains("unknown plugin 'missing'", ex.Message);
    }

    [Fact]
    public void Order_CyclicDependency_Throws()
    {
        var plugins = new[] { Plugin("alpha", "beta"), Plugin("beta", "alpha") };

        var ex = Assert.Throws<PluginLoadException>(() => PluginDependencyGraph.Order(plugins, NoDisabled));
        Assert.Contains("Cyclic", ex.Message);
    }

    [Fact]
    public void Order_SelfDependency_Throws()
    {
        var plugins = new[] { Plugin("alpha", "alpha") };

        Assert.Throws<PluginLoadException>(() => PluginDependencyGraph.Order(plugins, NoDisabled));
    }

    [Fact]
    public void Order_DisabledPlugin_IsSkipped()
    {
        var plugins = new[] { Plugin("alpha"), Plugin("gamma") };
        var disabled = new HashSet<string>(StringComparer.Ordinal) { "alpha" };

        var ordered = PluginDependencyGraph.Order(plugins, disabled);

        Assert.Equal(new[] { "gamma" }, ordered.Select(p => p.Name).ToArray());
    }

    [Fact]
    public void Order_DependencyOnDisabledPlugin_Throws()
    {
        var plugins = new[] { Plugin("alpha"), Plugin("beta", "alpha") };
        var disabled = new HashSet<string>(StringComparer.Ordinal) { "alpha" };

        var ex = Assert.Throws<PluginLoadException>(() => PluginDependencyGraph.Order(plugins, disabled));
        Assert.Contains("disabled plugin 'alpha'", ex.Message);
    }
}
