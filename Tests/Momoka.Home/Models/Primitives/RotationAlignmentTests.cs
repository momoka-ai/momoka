using Xunit;
using Momoka.Home.Primitives;
namespace Momoka.Home.Tests.Models.Primitives;

/// <summary>
/// 表面类别匹配规则：精确匹配之外仅 Horizontal 额外接受 Upside / Downside
/// （"水平"不区分上下）；Vertical / Tilted 严格匹配（方向 / 角度属物件自身姿态）。
/// </summary>
public class RotationAlignmentTests
{
    [Theory]
    [InlineData(RotationAlignment.Upside, RotationAlignment.Upside, true)]
    [InlineData(RotationAlignment.Upside, RotationAlignment.Downside, false)]
    [InlineData(RotationAlignment.Upside, RotationAlignment.Horizontal, false)] // 实际面可能朝下
    [InlineData(RotationAlignment.Upside, RotationAlignment.Vertical, false)]
    [InlineData(RotationAlignment.Upside, RotationAlignment.Tilted, false)]
    [InlineData(RotationAlignment.Horizontal, RotationAlignment.Upside, true)]
    [InlineData(RotationAlignment.Horizontal, RotationAlignment.Downside, true)]
    [InlineData(RotationAlignment.Horizontal, RotationAlignment.Horizontal, true)]
    [InlineData(RotationAlignment.Horizontal, RotationAlignment.Vertical, false)]
    [InlineData(RotationAlignment.Horizontal, RotationAlignment.Tilted, false)]
    [InlineData(RotationAlignment.Downside, RotationAlignment.Downside, true)]
    [InlineData(RotationAlignment.Downside, RotationAlignment.Upside, false)]
    [InlineData(RotationAlignment.Downside, RotationAlignment.Horizontal, false)]
    [InlineData(RotationAlignment.Vertical, RotationAlignment.Vertical, true)]
    [InlineData(RotationAlignment.Vertical, RotationAlignment.Upside, false)]
    [InlineData(RotationAlignment.Vertical, RotationAlignment.Tilted, false)]
    [InlineData(RotationAlignment.Tilted, RotationAlignment.Tilted, true)]
    [InlineData(RotationAlignment.Tilted, RotationAlignment.Vertical, false)]
    [InlineData(RotationAlignment.Tilted, RotationAlignment.Upside, false)]
    public void Matches_ExpectedCategoryVsActualSurface(
        RotationAlignment required, RotationAlignment actual, bool expected)
    {
        Assert.Equal(expected, required.Matches(actual));
    }
}
