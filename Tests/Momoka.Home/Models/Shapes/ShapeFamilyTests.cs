using Xunit;
using Momoka.Home.Levels.Volumes;
using Momoka.Home.Primitives;
namespace Momoka.Home.Tests.Models.Shapes;

/// <summary>
/// The extended shape family: prisms (extruded sections), composites, curved
/// lines and full-3D bodies. Shapes are pure local geometry.
/// </summary>
public class ShapeFamilyTests
{
    [Fact]
    public void Extruded3D_RectSectionTimesHeight_IsPrismVolume()
    {
        var prism = new Extruded(new[]
        {
            new Int2(0, 0), new Int2(1, 0),
            new Int2(0, 1), new Int2(1, 1),
            new Int2(0, 2), new Int2(1, 2),
        }, 4); // 2×3 截面（x∈0..1, z∈0..2）
        var cells = prism.GetVoxelSet().ToList();

        Assert.Equal(2 * 3 * 4, cells.Count);
        Assert.Contains(new Int3(1, 3, 2), cells);
    }

    [Fact]
    public void Polygon3D_ConcaveL_OmitsTheMissingCorner()
    {
        var poly = new Polygon(new[]
        {
            new Int2(0, 0), new Int2(3, 0), new Int2(3, 1),
            new Int2(1, 1), new Int2(1, 3), new Int2(0, 3),
        }, 1);

        var cells = poly.GetVoxelSet().Select(c => c.Xz).ToHashSet();

        Assert.Contains(new Int2(0, 0), cells);
        Assert.Contains(new Int2(2, 0), cells);
        Assert.Contains(new Int2(0, 2), cells);
        Assert.DoesNotContain(new Int2(2, 2), cells); // 缺失角
    }

    [Fact]
    public void Circle3D_And_Cylinder3D_SameVolume()
    {
        var circle = new Circle(3, 5);
        var cylinder = new Cylinder(3, 5);

        Assert.Equal(circle.GetVoxelSet().Count(), cylinder.GetVoxelSet().Count());
        Assert.True(circle.GetVoxelSet().Any());
    }

    [Fact]
    public void Composite3D_UnionsChildrenAtOffsets()
    {
        var composite = new Composite();
        composite.Children.Add(new CompositeChild { Shape = new Box { SizeX = 2, SizeY = 1, SizeZ = 2 }, Offset = Int3.Zero });
        composite.Children.Add(new CompositeChild { Shape = new Box { SizeX = 1, SizeY = 1, SizeZ = 1 }, Offset = new Int3(3, 0, 0) });

        var cells = composite.GetVoxelSet().ToHashSet();

        Assert.Contains(new Int3(0, 0, 0), cells);
        Assert.Contains(new Int3(1, 0, 1), cells);
        Assert.Contains(new Int3(3, 0, 0), cells); // 第二个子形状偏移后
        Assert.Equal(4 + 1, cells.Count);
    }

    [Fact]
    public void Curve3D_ZeroCurvature_MatchesStraightLine()
    {
        var line = new Line { Start = Float3.Zero, End = new Float3(6, 0, 0), Thickness = 1 };
        var curve = new Curve { Start = Float3.Zero, End = new Float3(6, 0, 0), Curvature = 0, Thickness = 1 };

        var expected = line.GetVoxelSet().Distinct().OrderBy(c => c.X).ThenBy(c => c.Z);
        var actual = curve.GetVoxelSet().Distinct().OrderBy(c => c.X).ThenBy(c => c.Z);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void Curve3D_PositiveCurvature_BowsAwayFromChord()
    {
        var curve = new Curve { Start = Float3.Zero, End = new Float3(6, 0, 0), Curvature = 2, Thickness = 1 }.GetVoxelSet().ToHashSet();

        Assert.Contains(new Int3(0, 0, 0), curve); // 起点
        Assert.Contains(new Int3(6, 0, 0), curve); // 终点
        Assert.Contains(curve, c => c.Z > 0);            // 向 +Z 侧弯曲
        Assert.DoesNotContain(new Int3(3, 0, 0), curve); // 直弦中点被外凸取代
    }

    [Fact]
    public void Cone3D_TapersToApex()
    {
        var cone = new Cone(3, 4);
        var cells = cone.GetVoxelSet().ToList();

        Assert.Contains(cells, c => c.Y == 0 && c.X == 0 && c.Z == 0); // 底心
        Assert.Equal(1, cells.Count(c => c.Y == 3));                   // 顶点层单格
    }

    [Fact]
    public void Pyramid3D_BaseMatchesBaseCells()
    {
        var pyramid = new Pyramid(4, 4, 3);
        var baseCells = pyramid.GetVoxelSet().Where(c => c.Y == 0).Select(c => c.Xz).ToHashSet();

        Assert.Equal(4 * 4, baseCells.Count);
    }

    [Fact]
    public void Sphere3D_CellsWithinRadius()
    {
        var sphere = new Sphere(2);
        var cells = sphere.GetVoxelSet().ToList();

        Assert.All(cells, c => Assert.True(c.X * c.X + c.Y * c.Y + c.Z * c.Z <= 4));
        Assert.Contains(new Int3(0, 0, 0), cells);
    }

}
