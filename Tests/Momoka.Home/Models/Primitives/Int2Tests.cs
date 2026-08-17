using Xunit;
using Momoka.Home.Primitives;
namespace Momoka.Home.Tests.Models.Primitives;

public class Int2Tests
{
    [Fact]
    public void Constants_AreZeroAndOne()
    {
        Assert.Equal(new Int2(0, 0), Int2.Zero);
        Assert.Equal(new Int2(1, 1), Int2.One);
    }

    [Fact]
    public void Arithmetic_Operators_CombineComponentWise()
    {
        Assert.Equal(new Int2(4, 8), new Int2(1, 3) + new Int2(3, 5));
        Assert.Equal(new Int2(-2, -2), new Int2(1, 3) - new Int2(3, 5));
        Assert.Equal(new Int2(2, 6), new Int2(1, 3) * 2);
        Assert.Equal(new Int2(2, 6), 2 * new Int2(1, 3));
        Assert.Equal(new Int2(1, 1), new Int2(5, 7) % 2);
        Assert.Equal(new Int2(1, 1), new Int2(5, 7) % new Int2(2, 3)); // 5%2=1, 7%3=1
    }

    [Fact]
    public void Offset_TranslatesByDelta()
    {
        Assert.Equal(new Int2(4, -2), new Int2(1, 3).Offset(3, -5));
    }

    [Fact]
    public void DistanceTo_UsesEuclideanDistance()
    {
        Assert.Equal(5.0, new Int2(0, 0).DistanceTo(new Int2(3, 4)));
    }

    [Fact]
    public void ManhattanDistance_SumsAbsoluteDeltas()
    {
        Assert.Equal(5, new Int2(1, 2).ManhattanDistance(new Int2(-3, 3))); // |1-(-3)|+|2-3|
    }

    [Fact]
    public void Neighbors4_YieldsCardinalCellsOnly()
    {
        var neighbors = new Int2(1, 2).Neighbors4().ToList();

        Assert.Equal(4, neighbors.Count);
        Assert.Contains(new Int2(0, 2), neighbors);
        Assert.Contains(new Int2(2, 2), neighbors);
        Assert.Contains(new Int2(1, 1), neighbors);
        Assert.Contains(new Int2(1, 3), neighbors);
        Assert.DoesNotContain(new Int2(0, 1), neighbors); // 无对角
    }

    [Fact]
    public void Neighbors8_IncludesDiagonals()
    {
        var neighbors = new Int2(1, 2).Neighbors8().ToList();

        Assert.Equal(8, neighbors.Count);
        Assert.Contains(new Int2(0, 1), neighbors);
        Assert.Contains(new Int2(2, 3), neighbors);
        Assert.DoesNotContain(new Int2(1, 2), neighbors); // 不含自身
    }

    [Fact]
    public void ExplicitConversion_FromInt3_DropsY()
    {
        Assert.Equal(new Int2(5, 7), (Int2)new Int3(5, 99, 7));
    }

    [Fact]
    public void ExplicitConversion_FromFloat3_RoundsXZ()
    {
        Assert.Equal(new Int2(1, 4), (Int2)new Float3(1.2f, 0f, 3.6f));
    }

    [Fact]
    public void LiftToInt3_CarriesGivenY()
    {
        Assert.Equal(new Int3(1, 5, 2), new Int2(1, 2).ToInt3(5));
        Assert.Equal(new Int3(1, 0, 2), new Int2(1, 2).ToInt3());
    }

    [Fact]
    public void LiftToFloat3_CarriesGivenY()
    {
        Assert.Equal(new Float3(1, 5, 2), new Int2(1, 2).ToFloat3(5f));
    }

    [Fact]
    public void LiftToVector3_CarriesGivenY()
    {
        Assert.Equal(new System.Numerics.Vector3(1, 5, 2), new Int2(1, 2).ToVector3(5f));
    }

    [Fact]
    public void ToString_FormatsComponents()
    {
        Assert.Equal("(1, 2)", new Int2(1, 2).ToString());
    }
}
