using Xunit;
using Momoka.Home;
using Momoka.Home.Geometry;
using Momoka.Home.Layouts;
using Momoka.Home.Primitives;
namespace Momoka.Home.Tests.Models.Layouts;

public class GridLayoutTests
{
    [Fact]
    public void IsCollided_Cell_TrueWhenBlocked_FalseWhenPlaceable()
    {
        var layout = new GridLayout<bool>(new Int2(5, 5));
        layout.Fill(true, new Int2(1, 1), new Int2(3, 3));

        Assert.True(layout.IsCollided(new Int2(0, 0)));   // 未填充 → 阻塞
        Assert.False(layout.IsCollided(new Int2(2, 2)));  // 已填充 → 可放置
        Assert.True(layout.IsCollided(new Int2(9, 9)));   // 越界 → 阻塞
    }

    [Fact]
    public void IsCollided_Shape_OnFreeArea_IsFalse()
    {
        var layout = new GridLayout<bool>(new Int2(10, 10));
        layout.Fill(true, new Int2(0, 0), new Int2(10, 10));

        var cabinet = new Box3D { SizeX = 2, SizeY = 3, SizeZ = 3 };
        Assert.False(layout.IsCollided(cabinet, new Int2(5, 5)));
    }

    [Fact]
    public void IsCollided_Shape_OverBlockedCell_IsTrue()
    {
        var layout = new GridLayout<bool>(new Int2(10, 10));
        layout.Fill(true, new Int2(0, 0), new Int2(10, 10));
        for (var x = 2; x <= 6; x++)
        {
            layout[new Int2(x, 0)] = false; // 模拟墙底阻挡
        }

        Assert.True(layout.IsCollided(new Box3D { SizeX = 2, SizeZ = 2 }, new Int2(2, 0)));
        Assert.False(layout.IsCollided(new Box3D { SizeX = 2, SizeZ = 2 }, new Int2(7, 0)));
    }

    [Fact]
    public void IsCollided_Shape_OutOfBounds_IsTrue()
    {
        var layout = new GridLayout<bool>(new Int2(10, 10));
        layout.Fill(true, new Int2(0, 0), new Int2(10, 10));

        Assert.True(layout.IsCollided(new Box3D { SizeX = 4, SizeZ = 4 }, new Int2(9, 9)));
    }
}
