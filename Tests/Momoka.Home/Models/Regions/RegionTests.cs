using Xunit;
using Momoka.Home.Levels;
using Momoka.Home.Primitives;
namespace Momoka.Home.Tests.Models.Regions;

/// <summary>
/// Region 是纯数据值类型（存于 <c>LevelLayout.Regions</c> 的 VoxelLayout），
/// 推导逻辑（原 Region.BuildLayout，ColumnLayout 时代）已删除——需要时按
/// VoxelLayout 重写。
/// </summary>
public class RegionTests
{
    [Fact]
    public void Region_DataDefaults_AndNameIsSettable()
    {
        var bounds = Bound.FromCorners(Int3.Zero.ToFloat3(), new Int3(4, 29, 4).ToFloat3());
        var region = new Region(7, bounds, 928, 25);

        Assert.Equal(7, region.Id);
        Assert.Equal(bounds, region.Bounds);
        Assert.Equal(928, region.Volume);
        Assert.Equal(25, region.Area);
        Assert.Equal("Region 7", region.Name); // 默认命名

        region.Name = "Bedroom";
        Assert.Equal("Bedroom", region.Name);
    }
}
