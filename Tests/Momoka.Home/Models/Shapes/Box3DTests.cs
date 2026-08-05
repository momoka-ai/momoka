using Xunit;
using Momoka.Home;
using Momoka.Home.Geometry;
using Momoka.Home.Primitives;
namespace Momoka.Home.Tests.Models.Shapes;

/// <summary>
/// Shapes are pure local geometry (no position); voxels and support footprints
/// are relative to the host entity's Coords.
/// </summary>
public class Box3DTests
{
    [Fact]
    public void Cabinet_Volume_IsThicknessTimesHeightTimesWidth()
    {
        // 2(thickness) × 3(height) × 3(width)
        var cabinet = new Box3D { SizeX = 2, SizeY = 3, SizeZ = 3 };

        Assert.Equal(2 * 3 * 3, cabinet.Cells3D().Count());
    }

    [Fact]
    public void Cabinet_FloorFootprint_IsThicknessTimesWidth()
    {
        var cabinet = new Box3D { SizeX = 2, SizeY = 3, SizeZ = 3 };
        var footprint = cabinet.Cells2D().ToList();

        Assert.Equal(2 * 3, footprint.Count); // 2×3
    }

    [Fact]
    public void Cabinet_WallFootprint_IsHeightTimesWidth_AfterOrientation()
    {
        // 贴墙时旋转：局部 XZ 覆盖墙面（高×宽），局部 Y 为厚度
        var wallCabinet = new Box3D { SizeX = 3, SizeY = 2, SizeZ = 3 };

        Assert.Equal(3 * 3, wallCabinet.Cells2D().Count()); // 3×3
    }

    [Fact]
    public void Cells_AreLocal_ZeroBased()
    {
        var box = new Box3D { SizeX = 2, SizeY = 1, SizeZ = 2 };
        var voxels = box.Cells3D().ToList();

        Assert.All(voxels, v => Assert.InRange(v.X, 0, 1));
        Assert.All(voxels, v => Assert.InRange(v.Z, 0, 1));
        Assert.Contains(new Int3(0, 0, 0), voxels);
        Assert.DoesNotContain(new Int3(5, 0, 0), voxels);
    }
}
