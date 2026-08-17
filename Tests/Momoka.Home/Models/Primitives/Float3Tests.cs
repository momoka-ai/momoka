using Xunit;
using Momoka.Home.Primitives;
namespace Momoka.Home.Tests.Models.Primitives;

public class Float3Tests
{
    [Fact]
    public void Constants_DeclareCardinalDirections()
    {
        Assert.Equal(new Float3(0f, 0f, 0f), Float3.Zero);
        Assert.Equal(new Float3(1f, 1f, 1f), Float3.One);
        Assert.Equal(new Float3(0f, 1f, 0f), Float3.Up);
        Assert.Equal(new Float3(0f, -1f, 0f), Float3.Down);
    }

    [Fact]
    public void ScalarConstructor_RepeatsAcrossAllAxes()
    {
        Assert.Equal(new Float3(3f, 3f, 3f), new Float3(3f));
    }

    [Fact]
    public void Arithmetic_Operators_CombineComponentWise()
    {
        Assert.Equal(new Float3(4, 6, 8), new Float3(1, 2, 3) + new Float3(3, 4, 5));
        Assert.Equal(new Float3(-2, -2, -2), new Float3(1, 2, 3) - new Float3(3, 4, 5));
        Assert.Equal(new Float3(2, 4, 6), new Float3(1, 2, 3) * 2f);
        Assert.Equal(new Float3(2, 4, 6), 2f * new Float3(1, 2, 3));
        Assert.Equal(new Float3(0.5f, 1f, 1.5f), new Float3(1, 2, 3) / 2f);
        Assert.Equal(new Float3(-1, -2, -3), -new Float3(1, 2, 3));
    }

    [Fact]
    public void Comparison_Operators_AreComponentWise()
    {
        Assert.True(new Float3(1, 2, 3) < new Float3(2, 3, 4));
        Assert.False(new Float3(1, 2, 3) < new Float3(1, 3, 4)); // X 相等 → 非严格小于
        Assert.True(new Float3(1, 2, 3) <= new Float3(1, 3, 4));
        Assert.True(new Float3(2, 3, 4) > new Float3(1, 2, 3));
        Assert.True(new Float3(1, 3, 4) >= new Float3(1, 2, 3));
        Assert.False(new Float3(1, 2, 3) > new Float3(1, 2, 3)); // 相等 → 非大于
    }

    [Fact]
    public void Magnitude_IsEuclideanLength()
    {
        Assert.Equal(5f, new Float3(3, 4, 0).Magnitude);
        Assert.Equal(0f, Float3.Zero.Magnitude);
    }

    [Fact]
    public void Normalized_ProducesUnitVector()
    {
        var normalized = new Float3(3, 4, 0).Normalized;

        Assert.True(Math.Abs(normalized.Magnitude - 1f) < 1e-6f);
        Assert.Equal(new Float3(0.6f, 0.8f, 0f), normalized);
    }

    [Fact]
    public void Normalized_OfZeroVector_IsZero()
    {
        Assert.Equal(Float3.Zero, Float3.Zero.Normalized);
    }

    [Fact]
    public void DistanceTo_IsEuclideanDistance()
    {
        Assert.Equal(5.0, new Float3(0, 0, 0).DistanceTo(new Float3(3, 4, 0)));
    }

    [Fact]
    public void Lerp_InterpolatesBetweenEndpoints()
    {
        Assert.Equal(new Float3(1, 2, 3), Float3.Lerp(new Float3(1, 2, 3), new Float3(5, 6, 7), 0f));
        Assert.Equal(new Float3(5, 6, 7), Float3.Lerp(new Float3(1, 2, 3), new Float3(5, 6, 7), 1f));
        Assert.Equal(new Float3(3, 4, 5), Float3.Lerp(new Float3(1, 2, 3), new Float3(5, 6, 7), 0.5f));
    }

    [Fact]
    public void Dot_IsZeroForOrthogonalVectors()
    {
        Assert.Equal(0f, Float3.Dot(new Float3(1, 0, 0), new Float3(0, 1, 0)));
        Assert.Equal(32f, Float3.Dot(new Float3(1, 2, 3), new Float3(4, 5, 6)));
    }

    [Fact]
    public void Cross_IsOrthogonal_AndAntiCommutative()
    {
        Assert.Equal(new Float3(0, 0, 1), Float3.Cross(new Float3(1, 0, 0), new Float3(0, 1, 0)));
        Assert.Equal(Float3.Cross(new Float3(0, 1, 0), new Float3(1, 0, 0)), -Float3.Cross(new Float3(1, 0, 0), new Float3(0, 1, 0)));
    }

    [Fact]
    public void SnapToGrid_RoundsToNearestGridLine()
    {
        Assert.Equal(new Float3(1f, 5f, -2f), new Float3(1.2f, 4.7f, -2.1f).SnapToGrid(1f));
        Assert.Equal(new Float3(10f, 0f, 30f), new Float3(11f, -1f, 28f).SnapToGrid(10f));
    }

    [Fact]
    public void Int3Floor_TruncatesTowardZero()
    {
        Assert.Equal(new Int3(1, -1, 3), new Float3(1.9f, -1.2f, 3.9f).Int3Floor);
    }

    [Fact]
    public void AsInt3_RoundsEachComponent()
    {
        Assert.Equal(new Int3(1, -1, 3), new Float3(1.4f, -0.6f, 3.4f).AsInt3());
    }

    [Fact]
    public void AsInt3F_Truncates()
    {
        Assert.Equal(new Int3(1, 2, 3), new Float3(1.9f, 2.1f, 3.9f).AsInt3F());
    }

    [Fact]
    public void AsInt2_RoundsXZ()
    {
        Assert.Equal(new Int2(1, 3), new Float3(1.4f, 99f, 3.4f).AsInt2());
    }

    [Fact]
    public void ToVector3_And_FromVector3_RoundTrip()
    {
        var v = new Float3(1, 2, 3).ToVector3();
        Assert.Equal(new Float3(1, 2, 3), Float3.FromVector3(v));
    }

    [Fact]
    public void ToString_FormatsWithThreeDecimals()
    {
        Assert.Equal("(1.000, 2.000, 3.000)", new Float3(1, 2, 3).ToString());
    }
}
