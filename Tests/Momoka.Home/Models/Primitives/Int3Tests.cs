using Xunit;
using Momoka.Home.Primitives;
namespace Momoka.Home.Tests.Models.Primitives;

public class Int3Tests
{
    [Fact]
    public void Constants_DeclareCardinalDirections()
    {
        Assert.Equal(new Int3(0, 0, 0), Int3.Zero);
        Assert.Equal(new Int3(1, 1, 1), Int3.One);
        Assert.Equal(new Int3(0, 1, 0), Int3.Up);
        Assert.Equal(new Int3(0, -1, 0), Int3.Down);
        Assert.Equal(new Int3(1, 0, 0), Int3.East);
        Assert.Equal(new Int3(-1, 0, 0), Int3.West);
        Assert.Equal(new Int3(0, 0, 1), Int3.North);
        Assert.Equal(new Int3(0, 0, -1), Int3.South);
    }

    [Fact]
    public void Arithmetic_Operators_CombineComponentWise()
    {
        Assert.Equal(new Int3(4, 6, 8), new Int3(1, 2, 3) + new Int3(3, 4, 5));
        Assert.Equal(new Int3(-2, -2, -2), new Int3(1, 2, 3) - new Int3(3, 4, 5));
        Assert.Equal(new Int3(2, 4, 6), new Int3(1, 2, 3) * 2);
        Assert.Equal(new Int3(2, 4, 6), 2 * new Int3(1, 2, 3));
        Assert.Equal(new Int3(1, 0, 1), new Int3(5, 6, 7) % 2);
        Assert.Equal(new Int3(1, 0, 1), new Int3(5, 6, 7) % new Int3(2, 3, 3));
    }

    [Fact]
    public void Offset_TranslatesByDelta()
    {
        Assert.Equal(new Int3(4, -1, 8), new Int3(1, 2, 3).Offset(3, -3, 5));
    }

    [Fact]
    public void DistanceTo_UsesEuclideanDistance()
    {
        Assert.Equal(5.0, new Int3(0, 0, 0).DistanceTo(new Int3(3, 4, 0)));
        Assert.Equal(0.0, new Int3(2, 2, 2).DistanceTo(new Int3(2, 2, 2)));
    }

    [Fact]
    public void ManhattanDistance_SumsAbsoluteDeltas()
    {
        Assert.Equal(10, new Int3(1, 2, 3).ManhattanDistance(new Int3(-2, 4, -2)));
    }

    [Fact]
    public void Neighbors6_YieldsAllFaceAdjacentCells()
    {
        var neighbors = new Int3(1, 2, 3).Neighbors6().ToList();

        Assert.Equal(6, neighbors.Count);
        Assert.Contains(new Int3(0, 2, 3), neighbors);
        Assert.Contains(new Int3(2, 2, 3), neighbors);
        Assert.Contains(new Int3(1, 1, 3), neighbors);
        Assert.Contains(new Int3(1, 3, 3), neighbors);
        Assert.Contains(new Int3(1, 2, 2), neighbors);
        Assert.Contains(new Int3(1, 2, 4), neighbors);
        Assert.DoesNotContain(new Int3(2, 3, 4), neighbors); // 无对角
    }

    [Fact]
    public void Range_EnumeratesInclusiveBox()
    {
        var cells = Int3.Range(new Int3(0, 1, 0), new Int3(1, 2, 2)).ToList();

        Assert.Equal(2 * 2 * 3, cells.Count); // (1-0+1)*(2-1+1)*(2-0+1)
        Assert.Contains(new Int3(0, 1, 0), cells);
        Assert.Contains(new Int3(1, 2, 2), cells);
        Assert.DoesNotContain(new Int3(2, 2, 2), cells);
    }

    [Fact]
    public void Xz_DropsTheYComponent()
    {
        Assert.Equal(new Int2(5, 7), new Int3(5, 9, 7).Xz);
    }

    [Fact]
    public void Conversions_ToFloat3_And_Vector3()
    {
        var f = new Int3(1, 2, 3).ToFloat3();
        var v = new Int3(1, 2, 3).ToVector3();

        Assert.Equal(new Float3(1, 2, 3), f);
        Assert.Equal(new System.Numerics.Vector3(1, 2, 3), v);
    }

    [Fact]
    public void ExplicitConversion_FromFloat3_RoundsToNearestEven()
    {
        // Math.Round 默认 banker's rounding：0.5 → 0、1.5 → 2
        Assert.Equal(new Int3(0, 0, 2), (Int3)new Float3(0.4f, 0.5f, 1.5f));
        Assert.Equal(new Int3(-1, -2, 2), (Int3)new Float3(-0.6f, -2.4f, 2.4f));
    }

    [Fact]
    public void ToString_FormatsComponents()
    {
        Assert.Equal("(1, 2, 3)", new Int3(1, 2, 3).ToString());
    }
}
