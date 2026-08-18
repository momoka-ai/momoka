using Xunit;
using Momoka.Home.Algorithms;
using Momoka.Home.Primitives;
namespace Momoka.Home.Tests.Models.Algorithms;

/// <summary>
/// 直线几何与圆锥形视野判定：点线分解（<see cref="Visibility.Project"/>）的
/// 投影距离 / 垂直分量，以及 <see cref="Visibility.IsInCone"/> 的距离与锥形边界
/// （半径随距离线性扩大——近端锥窄，与圆柱判定的关键差异）。
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

    // ── IsInCone：圆锥形视野包含判定（半径随距离线性扩大） ──

    [Fact]
    public void IsInCone_InsideCone_ReturnsTrue()
    {
        Assert.True(Visibility.IsInCone(new Float3(8, 0, 0), Float3.Zero, new Float3(1, 0, 0), 10f, 2f)); // 轴上
        Assert.True(Visibility.IsInCone(new Float3(6, 1, 0), Float3.Zero, new Float3(1, 0, 0), 10f, 2f)); // 垂距 1 ≤ 6/10×2
    }

    [Fact]
    public void IsInCone_BeyondMaxDistance_ReturnsFalse()
    {
        Assert.False(Visibility.IsInCone(new Float3(15, 0, 0), Float3.Zero, new Float3(1, 0, 0), 10f, 2f));
    }

    [Fact]
    public void IsInCone_NearConeIsNarrow_ReturnsFalse()
    {
        // 近端锥窄：同样垂距在远处成立、在近处越界（圆柱判定无此差异）
        Assert.False(Visibility.IsInCone(new Float3(3, 1, 0), Float3.Zero, new Float3(1, 0, 0), 10f, 2f)); // 1 > 3/10×2
        Assert.False(Visibility.IsInCone(new Float3(6, 2, 0), Float3.Zero, new Float3(1, 0, 0), 10f, 2f)); // 2 > 6/10×2
    }

    [Fact]
    public void IsInCone_BehindOrigin_ReturnsFalse()
    {
        Assert.False(Visibility.IsInCone(new Float3(-1, 0, 0), Float3.Zero, new Float3(1, 0, 0), 10f, 2f));
    }

    [Fact]
    public void IsInCone_OnBoundary_IsInclusive()
    {
        Assert.True(Visibility.IsInCone(new Float3(10, 0, 0), Float3.Zero, new Float3(1, 0, 0), 10f, 2f)); // 恰好最远
        Assert.True(Visibility.IsInCone(new Float3(10, 2, 0), Float3.Zero, new Float3(1, 0, 0), 10f, 2f)); // 恰好末端半径
        Assert.True(Visibility.IsInCone(new Float3(5, 1, 0), Float3.Zero, new Float3(1, 0, 0), 10f, 2f));  // 恰好线性边界
    }
}
