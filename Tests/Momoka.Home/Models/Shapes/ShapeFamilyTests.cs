using Xunit;
using Momoka.Home;
using Momoka.Home.Layouts;
using Momoka.Home.Primitives;
using Momoka.Home.Shapes;
namespace Momoka.Home.Tests.Models.Shapes;

/// <summary>
/// The extended shape family: 2D footprints (Polygon/Circle/…), prisms,
/// composites, curved lines and full-3D bodies. Shapes are pure local geometry.
/// </summary>
public class ShapeFamilyTests
{
    [Fact]
    public void Polygon2D_ConcaveL_OmitsTheMissingCorner()
    {
        var poly = new Polygon2D(
            new Int2(0, 0), new Int2(3, 0), new Int2(3, 1),
            new Int2(1, 1), new Int2(1, 3), new Int2(0, 3));

        var cells = poly.GetCells().ToHashSet();

        Assert.Contains(new Int2(0, 0), cells);
        Assert.Contains(new Int2(2, 0), cells);
        Assert.Contains(new Int2(0, 2), cells);
        Assert.DoesNotContain(new Int2(2, 2), cells); // 缺失角
    }

    [Fact]
    public void Circle2D_CellsStayWithinRadius()
    {
        var circle = new Circle2D(3);
        var cells = circle.GetCells().ToList();

        Assert.NotEmpty(cells);
        Assert.All(cells, c => Assert.True(c.X * c.X + c.Z * c.Z <= 9));
        Assert.Contains(new Int2(0, 0), cells);
        Assert.Contains(new Int2(3, 0), cells);
        Assert.DoesNotContain(new Int2(3, 3), cells);
    }

    [Fact]
    public void ExtrudedShape_RectFootprintTimesHeight_IsPrismVolume()
    {
        var prism = new ExtrudedShape(new Rect2D(2, 3), 4);
        var cells = prism.Cells().ToList();

        Assert.Equal(2 * 3 * 4, cells.Count);
        Assert.Equal(2 * 3, prism.GetVoxelsOnAngle().Count());
        Assert.Contains(new Int3(1, 3, 2), cells);
    }

    [Fact]
    public void CircleShape_And_CylinderShape_SameVolume()
    {
        var circle = new CircleShape(3, 5);
        var cylinder = new CylinderShape(3, 5);

        Assert.Equal(circle.Cells().Count(), cylinder.Cells().Count());
        Assert.True(circle.Cells().Any());
    }

    [Fact]
    public void CompositeShape_UnionsChildrenAtOffsets()
    {
        var composite = new CompositeShape();
        composite.Children.Add((new BoxShape { SizeX = 2, SizeY = 1, SizeZ = 2 }, Int3.Zero));
        composite.Children.Add((new BoxShape { SizeX = 1, SizeY = 1, SizeZ = 1 }, new Int3(3, 0, 0)));

        var cells = composite.Cells().ToHashSet();

        Assert.Contains(new Int3(0, 0, 0), cells);
        Assert.Contains(new Int3(1, 0, 1), cells);
        Assert.Contains(new Int3(3, 0, 0), cells); // 第二个子形状偏移后
        Assert.Equal(4 + 1, cells.Count);
        Assert.Equal(5, composite.GetVoxelsOnAngle().Count()); // 2×2 + 1
    }

    [Fact]
    public void CurveShape_ZeroCurvature_MatchesStraightLine()
    {
        var line = new LineShape { Start = Float3.Zero, End = new Float3(6, 0, 0), Thickness = 1 };
        var curve = new CurveShape { Start = Float3.Zero, End = new Float3(6, 0, 0), Curvature = 0, Thickness = 1 };

        var expected = line.Cells().Distinct().OrderBy(c => c.X).ThenBy(c => c.Z);
        var actual = curve.Cells().Distinct().OrderBy(c => c.X).ThenBy(c => c.Z);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void CurveShape_PositiveCurvature_BowsAwayFromChord()
    {
        var curve = new CurveShape { Start = Float3.Zero, End = new Float3(6, 0, 0), Curvature = 2, Thickness = 1 }.Cells().ToHashSet();

        Assert.Contains(new Int3(0, 0, 0), curve); // 起点
        Assert.Contains(new Int3(6, 0, 0), curve); // 终点
        Assert.Contains(curve, c => c.Z > 0);            // 向 +Z 侧弯曲
        Assert.DoesNotContain(new Int3(3, 0, 0), curve); // 直弦中点被外凸取代
    }

    [Fact]
    public void ConeShape_TapersToApex()
    {
        var cone = new ConeShape(3, 4);
        var cells = cone.Cells().ToList();

        Assert.Contains(cells, c => c.Y == 0 && c.X == 0 && c.Z == 0); // 底心
        Assert.Equal(1, cells.Count(c => c.Y == 3));                   // 顶点层单格
    }

    [Fact]
    public void PyramidShape_BaseMatchesFootprint()
    {
        var pyramid = new PyramidShape(4, 4, 3);
        var footprint = pyramid.GetVoxelsOnAngle().ToHashSet();
        var baseCells = pyramid.Cells().Where(c => c.Y == 0).Select(c => c.Xz).ToHashSet();

        Assert.Equal(baseCells, footprint);
        Assert.Equal(4 * 4, footprint.Count);
    }

    [Fact]
    public void SphereShape_CellsWithinRadius()
    {
        var sphere = new SphereShape(2);
        var cells = sphere.Cells().ToList();

        Assert.All(cells, c => Assert.True(c.X * c.X + c.Y * c.Y + c.Z * c.Z <= 4));
        Assert.Contains(new Int3(0, 0, 0), cells);
    }

    [Fact]
    public void Shape_PlaceAt_WithoutHost_Throws()
    {
        var box = new BoxShape { SizeX = 1, SizeY = 1, SizeZ = 1 };
        var layout = new VoxelLayout3D();

        Assert.Throws<NotSupportedException>(() => box.PlaceAt(layout, Int3.Zero));
    }
}
