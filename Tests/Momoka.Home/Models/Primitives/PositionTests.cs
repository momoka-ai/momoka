using Xunit;
using Momoka.Home.Primitives;
namespace Momoka.Home.Tests.Models.Primitives;

public class PositionTests
{
    [Fact]
    public void Zero_IsOriginAtUnitScale()
    {
        Assert.Equal(new Position(Float3.Zero), Position.Zero);
    }

    [Fact]
    public void DefaultScale_IsOne_CmCoordinates()
    {
        var position = new Position(new Float3(20, 10, 30));

        Assert.Equal(1f, position.Scale);
        Assert.Equal(new Float3(20, 10, 30), position.Pos);
        Assert.Equal(new Float3(20, 10, 30), position.Absolute());
    }

    [Fact]
    public void VoxelScale_Absolute_MultipliesByScale()
    {
        var position = new Position(new Int3(2, 3, 4), 10f);

        Assert.Equal(10f, position.Scale);
        Assert.Equal(new Float3(2, 3, 4), position.Pos);
        Assert.Equal(new Float3(20, 30, 40), position.Absolute());
    }

    [Fact]
    public void Normalized_IsPosTimesScale()
    {
        var position = new Position(new Int3(2, 3, 4), 10f);

        Assert.Equal(new Float3(20, 30, 40), position.Normalized);
    }

    [Fact]
    public void AsInt3_RoundsAwayFromZero()
    {
        // 在自身尺度内取整：0.5 → 1、-0.5 → -1（AwayFromZero，非 banker's）
        Assert.Equal(new Int3(1, -1, 3), new Position(new Float3(0.5f, -0.5f, 3.2f)).AsInt3());
    }

    [Fact]
    public void AsFloat3_ReturnsTheRawVector()
    {
        var position = new Position(new Float3(1, 2, 3), 10f);

        Assert.Equal(new Float3(1, 2, 3), position.AsFloat3());
    }

    [Fact]
    public void Rescale_ReexpressesTheSamePoint()
    {
        var voxels = new Position(new Int3(2, 3, 4), 10f);
        var centimeters = voxels.Rescale(1f);

        Assert.Equal(1f, centimeters.Scale);
        Assert.Equal(new Float3(20, 30, 40), centimeters.Pos);
        Assert.Equal(voxels.Absolute(), centimeters.Absolute());
    }

    [Fact]
    public void Equality_IsBasedOnAbsolutePosition()
    {
        var a = new Position(new Float3(2, 3, 4), 10f);
        var b = new Position(new Float3(20, 30, 40), 1f);

        Assert.Equal(a, b); // 不同尺度、同一点
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }

    [Fact]
    public void Add_Float3_OffsetsInSameScale()
    {
        var result = new Position(new Float3(1, 2, 3), 10f) + new Float3(1, 0, 0);

        Assert.Equal(new Position(new Float3(2, 2, 3), 10f), result);
    }

    [Fact]
    public void Subtract_Float3_OffsetsInSameScale()
    {
        var result = new Position(new Float3(1, 2, 3), 10f) - new Float3(1, 0, 0);

        Assert.Equal(new Position(new Float3(0, 2, 3), 10f), result);
    }

    [Fact]
    public void Subtract_Position_ReturnsAbsoluteDeltaAtUnitScale()
    {
        var delta = new Position(new Float3(50, 0, 0), 1f) - new Position(new Int3(2, 0, 0), 10f);

        Assert.Equal(1f, delta.Scale);
        Assert.Equal(new Float3(30, 0, 0), delta.Absolute());
    }

    [Fact]
    public void ComponentAccessors_ReturnPosComponents()
    {
        var position = new Position(new Float3(1, 2, 3));

        Assert.Equal(1f, position.X);
        Assert.Equal(2f, position.Y);
        Assert.Equal(3f, position.Z);
    }
}
