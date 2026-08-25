using Xunit;
using Momoka.Core.Plugins;

namespace Momoka.Core.Tests;

/// <summary>PluginInfo（plugin.toml 直接反序列化）与依赖图纯函数：解析/缺字段/重复名/未知依赖/依赖环/排序/软前置。</summary>
public sealed class PluginInfoTests
{
    private static PluginInfo Plugin(string name, params string[] dependencies) =>
        new()
        {
            Name = name,
            Version = "1.0.0",
            Main = $"{name}.Entry, Fake",
            Dependency = dependencies,
        };

    [Fact]
    public void Parse_ValidManifest_ReturnsFields()
    {
        const string toml = """
            name = "home"
            version = "1.2.3"
            main = "Momoka.Home.HomePlugin, Momoka.Home"
            dependency = ["ai", "sense"]
            dependencyOptional = ["vision"]
            authors = ["alice", "bob"]
            description = "Home companion plugin"
            api = "2.1"
            """;

        var info = PluginInfo.Parse(toml, "plugin.toml");

        Assert.Equal("home", info.Name);
        Assert.Equal("1.2.3", info.Version);
        Assert.Equal("Momoka.Home.HomePlugin, Momoka.Home", info.Main);
        Assert.Equal(new[] { "ai", "sense" }, info.Dependency);
        Assert.Equal(new[] { "vision" }, info.DependencyOptional);
        Assert.Equal(new[] { "alice", "bob" }, info.Authors);
        Assert.Equal("Home companion plugin", info.Description);
        Assert.Equal(new Version(2, 1), info.Api);
    }

    [Fact]
    public void Parse_OptionalFields_Defaults()
    {
        const string toml = """
            name = "home"
            version = "1.0.0"
            main = "Momoka.Home.HomePlugin, Momoka.Home"
            """;

        var info = PluginInfo.Parse(toml, "plugin.toml");

        Assert.Empty(info.Dependency);
        Assert.Empty(info.DependencyOptional);
        Assert.Empty(info.Authors);
        Assert.Empty(info.Description);
        Assert.Equal(new Version(1, 0), info.Api);
    }

    [Fact]
    public void Parse_IgnoresNonManifestKeys()
    {
        const string toml = """
            name = "home"
            version = "1.0.0"
            main = "Momoka.Home.HomePlugin, Momoka.Home"
            settings = { anything = 1 }
            """;

        var info = PluginInfo.Parse(toml, "plugin.toml");

        Assert.Equal("home", info.Name);
    }

    [Theory]
    [InlineData("name = \"x\"\nversion = \"1\"\n")]
    [InlineData("name = \"x\"\nmain = \"a.b\"\n")]
    [InlineData("version = \"1\"\nmain = \"a.b\"\n")]
    [InlineData("name = \"\"\nversion = \"1\"\nmain = \"a.b\"\n")]
    public void Parse_MissingRequiredField_Throws(string toml)
    {
        Assert.Throws<InvalidInfoException>(() => PluginInfo.Parse(toml, "plugin.toml"));
    }

    [Fact]
    public void Parse_InvalidSyntax_Throws()
    {
        Assert.Throws<InvalidInfoException>(() => PluginInfo.Parse("name = [", "plugin.toml"));
    }

    [Fact]
    public void Parse_WrongFieldType_Throws()
    {
        const string toml = """
            name = 42
            version = "1"
            main = "a.b"
            """;

        Assert.Throws<InvalidInfoException>(() => PluginInfo.Parse(toml, "plugin.toml"));
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

        var ordered = PluginDependencyGraph.Order(plugins);

        Assert.Equal(new[] { "alpha", "beta", "gamma" }, ordered.Select(p => p.Name).ToArray());
    }

    [Fact]
    public void Order_OptionalDependency_ParticipatesInOrdering()
    {
        var plugins = new[]
        {
            Plugin("beta"),
            Plugin("alpha"),
        };
        plugins[0].DependencyOptional = new[] { "alpha" };

        var ordered = PluginDependencyGraph.Order(plugins);

        Assert.Equal(new[] { "alpha", "beta" }, ordered.Select(p => p.Name).ToArray());
    }

    [Fact]
    public void Order_OptionalDependencyOnUnknown_IsIgnored()
    {
        var plugins = new[]
        {
            Plugin("beta"),
            Plugin("alpha"),
        };
        plugins[0].DependencyOptional = new[] { "missing" };

        var ordered = PluginDependencyGraph.Order(plugins);

        Assert.Equal(new[] { "beta", "alpha" }, ordered.Select(p => p.Name).ToArray());
    }

    [Fact]
    public void Order_DuplicateNames_Throws()
    {
        var plugins = new[] { Plugin("alpha"), Plugin("alpha") };

        var ex = Assert.Throws<InvalidPluginException>(() => PluginDependencyGraph.Order(plugins));
        Assert.Contains("Duplicate", ex.Message);
    }

    [Fact]
    public void Order_UnknownDependency_Throws()
    {
        var plugins = new[] { Plugin("beta", "missing") };

        var ex = Assert.Throws<UnknownDependencyException>(() => PluginDependencyGraph.Order(plugins));
        Assert.Contains("unknown plugin 'missing'", ex.Message);
    }

    [Fact]
    public void Order_CyclicDependency_Throws()
    {
        var plugins = new[] { Plugin("alpha", "beta"), Plugin("beta", "alpha") };

        var ex = Assert.Throws<InvalidPluginException>(() => PluginDependencyGraph.Order(plugins));
        Assert.Contains("Cyclic", ex.Message);
    }

    [Fact]
    public void Order_CycleViaOptionalDependency_Throws()
    {
        var plugins = new[] { Plugin("alpha"), Plugin("beta") };
        plugins[0].DependencyOptional = new[] { "beta" };
        plugins[1].DependencyOptional = new[] { "alpha" };

        var ex = Assert.Throws<InvalidPluginException>(() => PluginDependencyGraph.Order(plugins));
        Assert.Contains("Cyclic", ex.Message);
    }

    [Fact]
    public void Order_SelfDependency_Throws()
    {
        var plugins = new[] { Plugin("alpha", "alpha") };

        Assert.Throws<InvalidPluginException>(() => PluginDependencyGraph.Order(plugins));
    }

}
