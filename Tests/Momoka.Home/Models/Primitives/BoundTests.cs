using Xunit;
using Momoka.Home.Primitives;
namespace Momoka.Home.Tests.Models.Primitives;

public class BoundTests
{
    [Fact]
    public void UnsetValue_IsInvalid()
    {
        Assert.False(Bound.UnsetValue.Valid);
    }

    [Fact]
    public void Valid_NormalBound()
    {
        Assert.True(new Bound(new Float3(0, 0, 0), new Float3(10, 20, 30)).Valid);
    }

    [Fact]
    public void Valid_False_WhenMinExceedsMax()
    {
        Assert.False(new Bound(new Float3(10, 0, 0), new Float3(0, 0, 0)).Valid);
        Assert.False(new Bound(new Float3(0, 10, 0), new Float3(0, 0, 0)).Valid);
        Assert.False(new Bound(new Float3(0, 0, 10), new Float3(0, 0, 0)).Valid);
    }

    [Fact]
    public void Valid_False_OutsideWorldExtent()
    {
        Assert.False(new Bound(new Float3(-200000, 0, 0), new Float3(0, 0, 0)).Valid);
    }

    [Fact]
    public void IsValid_AcceptsWorldExtentBoundary()
    {
        Assert.True(Bound.IsValid(163840.0f));
        Assert.True(Bound.IsValid(-163840.0f));
        Assert.False(Bound.IsValid(163841.0f));
    }

    [Fact]
    public void FromCorners_NormalizesReversedCorners()
    {
        var bound = Bound.FromCorners(new Float3(10, 20, 30), new Float3(0, 5, 15));

        Assert.Equal(new Float3(0, 5, 15), bound.Min);
        Assert.Equal(new Float3(10, 20, 30), bound.Max);
        Assert.True(bound.Valid);
    }

    [Fact]
    public void Size_IsInclusiveSpan()
    {
        var bound = new Bound(new Float3(0, 0, 0), new Float3(9, 19, 29));

        Assert.Equal(10f, bound.SizeX);
        Assert.Equal(20f, bound.SizeY);
        Assert.Equal(30f, bound.SizeZ);
        Assert.Equal(new Float3(10, 20, 30), bound.Size);
        Assert.Equal(10f * 20f * 30f, bound.Volume);
    }

    [Fact]
    public void Center_IsMidpoint()
    {
        var bound = new Bound(new Float3(0, 0, 0), new Float3(10, 20, 30));

        Assert.Equal(new Float3(5, 10, 15), bound.Center);
    }

    [Fact]
    public void Contains_Point_InclusiveOnBoundary()
    {
        var bound = new Bound(new Float3(0, 0, 0), new Float3(10, 10, 10));

        Assert.True(bound.Contains(new Float3(0, 0, 0)));
        Assert.True(bound.Contains(new Float3(10, 10, 10)));
        Assert.True(bound.Contains(new Float3(5, 5, 5)));
        Assert.False(bound.Contains(new Float3(11, 5, 5)));
        Assert.False(bound.Contains(new Float3(5, -1, 5)));
    }

    [Fact]
    public void Contains_Bound_RequiresFullyInside()
    {
        var outer = new Bound(new Float3(0, 0, 0), new Float3(10, 10, 10));
        var inner = new Bound(new Float3(1, 1, 1), new Float3(9, 9, 9));
        var stickingOut = new Bound(new Float3(1, 1, 1), new Float3(11, 9, 9));

        Assert.True(outer.Contains(inner));
        Assert.False(outer.Contains(stickingOut));
    }

    [Fact]
    public void Intersects_TouchingBoundsCount()
    {
        var a = new Bound(new Float3(0, 0, 0), new Float3(10, 10, 10));
        var touching = new Bound(new Float3(10, 0, 0), new Float3(20, 10, 10));
        var disjoint = new Bound(new Float3(11, 0, 0), new Float3(20, 10, 10));

        Assert.True(a.Intersects(touching));
        Assert.False(a.Intersects(disjoint));
    }

    [Fact]
    public void Union_EnclosesBothBounds()
    {
        var a = new Bound(new Float3(0, 0, 0), new Float3(10, 10, 10));
        var b = new Bound(new Float3(20, 5, 0), new Float3(30, 15, 10));

        var union = a.Union(b);

        Assert.Equal(new Float3(0, 0, 0), union.Min);
        Assert.Equal(new Float3(30, 15, 10), union.Max);
    }

    [Fact]
    public void Intersect_ReturnsOverlap()
    {
        var a = new Bound(new Float3(0, 0, 0), new Float3(10, 10, 10));
        var b = new Bound(new Float3(5, 5, 5), new Float3(20, 20, 20));

        var intersection = a.Intersect(b);

        Assert.Equal(new Float3(5, 5, 5), intersection.Min);
        Assert.Equal(new Float3(10, 10, 10), intersection.Max);
    }

    [Fact]
    public void Intersect_Disjoint_ReturnsUnsetValue()
    {
        var a = new Bound(new Float3(0, 0, 0), new Float3(10, 10, 10));
        var b = new Bound(new Float3(20, 20, 20), new Float3(30, 30, 30));

        Assert.Equal(Bound.UnsetValue, a.Intersect(b));
    }

    [Fact]
    public void Constructors_FromIntsAndFloats_Agree()
    {
        Assert.Equal(new Bound(new Float3(0, 0, 0), new Float3(10, 20, 30)),
            new Bound(0f, 0f, 0f, 10f, 20f, 30f));
        Assert.Equal(new Bound(new Float3(0, 0, 0), new Float3(10, 0, 20)),
            new Bound(new Int3(0, 0, 0), new Int3(10, 0, 20)));
    }

    [Fact]
    public void ToString_FormatsCorners()
    {
        Assert.Equal("[(0.000, 0.000, 0.000) .. (10.000, 20.000, 30.000)]",
            new Bound(new Float3(0, 0, 0), new Float3(10, 20, 30)).ToString());
    }
}
