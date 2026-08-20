using Xunit;
using Momoka.Home.Primitives;
namespace Momoka.Home.Tests.Models.Primitives;

public class KeyTests
{
    [Fact]
    public void Constructor_AcceptsLowercaseNamespaceAndPath()
    {
        var key = new Key("midea", "air_conditioner.ac_1523");

        Assert.Equal("midea", key.Namespace);
        Assert.Equal("air_conditioner.ac_1523", key.Path);
    }

    [Fact]
    public void Constructor_InvalidNamespace_Throws()
    {
        Assert.Throws<ArgumentException>(() => new Key("Midea", "door"));
        Assert.Throws<ArgumentException>(() => new Key("my ns", "door"));
        Assert.Throws<ArgumentException>(() => new Key("", "door"));
    }

    [Fact]
    public void Constructor_InvalidPath_Throws()
    {
        Assert.Throws<ArgumentException>(() => new Key("momoka", "My Door"));
        Assert.Throws<ArgumentException>(() => new Key("momoka", "door:left"));
    }

    [Fact]
    public void Parse_WithNamespace_SplitsOnColon()
    {
        var key = Key.Parse("momoka:door");

        Assert.Equal("momoka", key.Namespace);
        Assert.Equal("door", key.Path);
    }

    [Fact]
    public void Parse_WithoutNamespace_UsesDefault()
    {
        var key = Key.Parse("door");

        Assert.Equal("momoka", key.Namespace);
        Assert.Equal("door", key.Path);
    }

    [Fact]
    public void Parse_MultipleColons_Throws()
    {
        Assert.Throws<ArgumentException>(() => Key.Parse("a:b:c"));
    }

    [Fact]
    public void TryParse_ReturnsTrue_ForValidInput()
    {
        Assert.True(Key.TryParse("momoka:door", out var key));
        Assert.Equal(new Key("momoka", "door"), key);
    }

    [Fact]
    public void TryParse_ReturnsFalse_ForInvalidInput()
    {
        Assert.False(Key.TryParse("Invalid:door", out _));
        Assert.False(Key.TryParse("a:b:c", out _));
    }

    [Fact]
    public void ToString_JoinsWithColon()
    {
        Assert.Equal("momoka:door", new Key("momoka", "door").ToString());
    }

    [Fact]
    public void CompareTo_OrdersByNamespaceThenPath()
    {
        Assert.True(new Key("a", "x").CompareTo(new Key("b", "x")) < 0);
        Assert.True(new Key("a", "x").CompareTo(new Key("a", "y")) < 0);
        Assert.True(new Key("a", "y").CompareTo(new Key("a", "x")) > 0);
        Assert.Equal(0, new Key("a", "x").CompareTo(new Key("a", "x")));
    }

    [Fact]
    public void ComparisonOperators_FollowCompareTo()
    {
        Assert.True(new Key("a", "x") < new Key("b", "x"));   // ns 序
        Assert.True(new Key("a", "x") <= new Key("a", "y"));  // 同 ns，path 序
        Assert.True(new Key("b", "x") > new Key("a", "x"));   // ns 反序
        Assert.True(new Key("a", "y") >= new Key("a", "x"));  // 同 ns，path 反序
    }

    [Fact]
    public void ImplicitConversion_FromString_UsesDefaultNamespace()
    {
        Key key = "door";

        Assert.Equal(new Key("momoka", "door"), key);
    }

    [Fact]
    public void Equality_IsValueBased()
    {
        Assert.Equal(new Key("momoka", "door"), Key.Parse("door"));
        Assert.NotEqual(new Key("momoka", "door"), new Key("midea", "door"));
    }
}
