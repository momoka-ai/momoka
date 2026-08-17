using Xunit;
using Momoka.Home.Algorithms;
using Momoka.Home.Primitives;
namespace Momoka.Home.Tests.Models.Algorithms;

/// <summary>
/// 直线几何与圆柱形视野判定：点线分解（<see cref="Visibility.Project"/>）的
/// 投影距离 / 垂直分量，以及 <see cref="Visibility.IsInView"/> 的距离与半径边界。
/// </summary>
public class VisibilityTests
{
    // ── Project：点对直线分解 ─────────────────────────────

    [Fact]
    public void Project_PointOnAxis_NoLateralComponent()
    {
        var p = Visibility.Project(new Float3(3, 0, 0), Float3.Zero, new Float3(1, 0, 0));

        Assert.Equal(3f, p.Distance);
        Assert.Equal(Float3.Zero, p.Lateral);
        Assert.Equal(0f, p.LateralDistance);
    }

    [Fact]
    public void Project_PointOffAxis_LateralIsPerpendicular()
    {
        var p = Visibility.Project(new Float3(3, 2, 0), Float3.Zero, new Float3(1, 0, 0));

        Assert.Equal(3f, p.Distance);
        Assert.Equal(new Float3(0, 2, 0), p.Lateral);
        Assert.Equal(2f, p.LateralDistance);
    }

    [Fact]
    public void Project_PointBehindOrigin_NegativeDistance()
    {
        var p = Visibility.Project(new Float3(-2, 0, 0), Float3.Zero, new Float3(1, 0, 0));

        Assert.Equal(-2f, p.Distance);
    }

    [Fact]
    public void Project_NonZeroOrigin_ProjectsRelativeToOrigin()
    {
        var p = Visibility.Project(new Float3(1, 5, 1), new Float3(1, 1, 1), new Float3(0, 1, 0));

        Assert.Equal(4f, p.Distance);
        Assert.Equal(new Float3(0, 0, 0), p.Lateral);
    }

    // ── IsInView：圆柱形视野包含判定 ──────────────────────

    [Fact]
    public void IsInView_InsideCylinder_ReturnsTrue()
    {
        Assert.True(Visibility.IsInView(new Float3(3, 0, 0), Float3.Zero, new Float3(1, 0, 0), 10f, 2f));
        Assert.True(Visibility.IsInView(new Float3(3, 1, 1), Float3.Zero, new Float3(1, 0, 0), 10f, 2f));
    }

    [Fact]
    public void IsInView_BeyondMaxDistance_ReturnsFalse()
    {
        Assert.False(Visibility.IsInView(new Float3(15, 0, 0), Float3.Zero, new Float3(1, 0, 0), 10f, 2f));
    }

    [Fact]
    public void IsInView_BeyondRadius_ReturnsFalse()
    {
        Assert.False(Visibility.IsInView(new Float3(3, 3, 0), Float3.Zero, new Float3(1, 0, 0), 10f, 2f));
    }

    [Fact]
    public void IsInView_BehindOrigin_ReturnsFalse()
    {
        Assert.False(Visibility.IsInView(new Float3(-1, 0, 0), Float3.Zero, new Float3(1, 0, 0), 10f, 2f));
    }

    [Fact]
    public void IsInView_OnBoundary_IsInclusive()
    {
        Assert.True(Visibility.IsInView(new Float3(10, 0, 0), Float3.Zero, new Float3(1, 0, 0), 10f, 2f)); // 恰好最远
        Assert.True(Visibility.IsInView(new Float3(3, 2, 0), Float3.Zero, new Float3(1, 0, 0), 10f, 2f)); // 恰好半径
    }
}
