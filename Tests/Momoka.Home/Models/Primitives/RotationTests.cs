using Xunit;
using Momoka.Home.Primitives;
namespace Momoka.Home.Tests.Models.Primitives;

/// <summary>
/// 三轴旋转原语（<see cref="Rotation"/>，内旋 YXZ 与 Godot 默认一致）：
/// 预定义 6 向法向 / 行轴 / 列轴、轴对齐与类别判定、Godot 弧度映射。
/// </summary>
public class RotationTests
{
    private static void AssertNear(Float3 expected, Float3 actual, float tolerance = 1e-3f) =>
        Assert.True((expected - actual).Magnitude <= tolerance, $"expected {expected}, got {actual}");

    [Theory]
    [InlineData(0, 0, 0, 0, 1, 0)]      // Up
    [InlineData(0, 180, 0, 0, -1, 0)]   // Down
    [InlineData(0, 90, 0, 0, 0, 1)]     // North
    [InlineData(180, 90, 0, 0, 0, -1)]  // South
    [InlineData(90, 90, 0, 1, 0, 0)]    // East
    [InlineData(-90, 90, 0, -1, 0, 0)]  // West
    public void AxisAligned_Normal_MatchesInt3Directions(float yaw, float pitch, float roll, float nx, float ny, float nz)
    {
        AssertNear(new Float3(nx, ny, nz), new Rotation(yaw, pitch, roll).Normal);
    }

    [Fact]
    public void Presets_EqualAxisAlignedValues()
    {
        Assert.Equal(Rotation.Up, Rotation.Identity);
        AssertNear(Float3.Up, Rotation.Up.Normal);
        AssertNear(-Float3.Up, Rotation.Down.Normal);
        AssertNear(new Float3(0, 0, 1), Rotation.North.Normal);   // Int3.North = +Z
        AssertNear(new Float3(0, 0, -1), Rotation.South.Normal);
        AssertNear(new Float3(1, 0, 0), Rotation.East.Normal);    // Int3.East = +X
        AssertNear(new Float3(-1, 0, 0), Rotation.West.Normal);
    }

    [Fact]
    public void Identity_RowAndColumnAxes_AreWorldXAndZ()
    {
        AssertNear(new Float3(1, 0, 0), Rotation.Identity.RowAxis);
        AssertNear(new Float3(0, 0, 1), Rotation.Identity.ColumnAxis); // Up 面列轴沿 +Z
    }

    [Fact]
    public void UpSurface_RowAxis_MatchesLegacyDirection()
    {
        // roll=0 时行轴退化为仅随 yaw：与旧 Direction.RowAxis 公式一致
        AssertNear(new Float3(1, 0, 0), new Rotation(0, 0, 0).RowAxis);
        AssertNear(new Float3(0, 0, -1), new Rotation(90, 90, 0).RowAxis); // East 面
        AssertNear(new Float3(-1, 0, 0), new Rotation(180, 90, 0).RowAxis); // South 面
    }

    [Fact]
    public void Roll_RotatesRowAxis_WithoutChangingNormal()
    {
        // 绕法向自转：法向不变；行轴随 roll 旋转（roll=90° 时行轴指向法向，
        // 表面行列概念退化——行轴与列轴、法向不再两两垂直）
        var r = new Rotation(0, 0, 90);
        AssertNear(Float3.Up, r.Normal);
        AssertNear(new Float3(0, 1, 0), r.RowAxis);   // (1,0,0) 绕 Z 转 90° → (0,1,0)
        AssertNear(new Float3(0, 0, 1), r.ColumnAxis); // 绕列轴自转不改变列轴
    }

    [Fact]
    public void RollOnHorizontalSurface_TurnsRowAxisOnly()
    {
        // 桌面上的物件转 45°：法向与列轴不变，行轴随 roll 旋转（水平面内）
        var r = new Rotation(0, 0, 45);
        var c = MathF.Cos(45 * MathF.PI / 180);
        var s = MathF.Sin(45 * MathF.PI / 180);
        AssertNear(Float3.Up, r.Normal);
        AssertNear(new Float3(c, s, 0), r.RowAxis);
        AssertNear(new Float3(0, 0, 1), r.ColumnAxis);
    }

    [Theory]
    [InlineData(0, 0, 0, RotationAlignment.Upside)]
    [InlineData(0, 180, 0, RotationAlignment.Downside)]
    [InlineData(0, 90, 0, RotationAlignment.Vertical)]
    [InlineData(90, 90, 0, RotationAlignment.Vertical)]
    [InlineData(0, 45, 0, RotationAlignment.Tilted)]
    public void Alignment_ClassifiesByNormalY(float yaw, float pitch, float roll, RotationAlignment expected)
    {
        Assert.Equal(expected, new Rotation(yaw, pitch, roll).Alignment);
    }

    [Fact]
    public void IsAxisAligned_ToleranceOnQuarterTurns()
    {
        Assert.True(Rotation.Up.IsAxisAligned);
        Assert.True(new Rotation(90, 180, -90).IsAxisAligned);
        Assert.False(new Rotation(45, 0, 0).IsAxisAligned);
        Assert.False(new Rotation(0, 0, 30).IsAxisAligned);
    }

    [Fact]
    public void GodotRadians_RoundTrips()
    {
        var r = new Rotation(30, 45, 60);
        var godot = r.ToGodotRadians();
        // Godot Vector3(x=pitch, y=yaw, z=roll)，弧度
        Assert.Equal(45f * MathF.PI / 180, godot.X, 5);
        Assert.Equal(30f * MathF.PI / 180, godot.Y, 5);
        Assert.Equal(60f * MathF.PI / 180, godot.Z, 5);
        Assert.Equal(r, Rotation.FromGodotRadians(godot));
    }

    [Fact]
    public void GodotRadians_Identity_IsZero()
    {
        Assert.Equal(Float3.Zero, Rotation.Identity.ToGodotRadians());
        Assert.Equal(Rotation.Identity, Rotation.FromGodotRadians(Float3.Zero));
    }
}
