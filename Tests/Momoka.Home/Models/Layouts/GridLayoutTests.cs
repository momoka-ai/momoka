using Xunit;
using Momoka.Home.Levels.Layouts;
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
}
