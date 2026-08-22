using Xunit;
using Momoka.Home.Levels.Volumes;
using Momoka.Home.Primitives;
using Momoka.Home.Data.Json;
using Momoka.Home.Data.Json.Converters;
using Newtonsoft.Json;
namespace Momoka.Home.Tests.Models.Serialization;

/// <summary>
/// Round-trips every <see cref="Volume"/> kind through
/// <see cref="JsonGeometryConverter"/>. Locks the declarative JSON format so the
/// registry/codec rewrite stays format-compatible.
/// </summary>
public class JsonGeometryConverterTests
{
    private static readonly JsonSerializerSettings Settings = new()
    {
        Converters = { new JsonGeometryConverter() }
    };

    private static T RoundTrip<T>(T volume) where T : Volume =>
        (T)JsonConvert.DeserializeObject(JsonConvert.SerializeObject(volume, Settings), typeof(T), Settings)!;

    [Fact]
    public void Box_RoundTrips()
    {
        var box = RoundTrip(new Box { SizeX = 1, SizeY = 2, SizeZ = 3 });
        Assert.Equal(1, box.SizeX);
        Assert.Equal(2, box.SizeY);
        Assert.Equal(3, box.SizeZ);
    }

    [Fact]
    public void Line_RoundTrips()
    {
        var line = RoundTrip(new Line { Start = new Float3(1, 2, 3), End = new Float3(6, 2, 0), Thickness = 2 });
        Assert.Equal(new Float3(1, 2, 3), line.Start);
        Assert.Equal(new Float3(6, 2, 0), line.End);
        Assert.Equal(2, line.Thickness);
    }

    [Fact]
    public void Curve_RoundTrips()
    {
        var curve = RoundTrip(new Curve { Start = new Float3(0, 0, 0), End = new Float3(6, 0, 0), Curvature = 2, Thickness = 1 });
        Assert.Equal(2, curve.Curvature);
        Assert.Equal(6, curve.End.X);
    }

    [Fact]
    public void Triangle_RoundTrips()
    {
        var triangle = RoundTrip(new Triangle(new Int2(0, 0), new Int2(2, 0), new Int2(0, 2), 3));
        Assert.Equal(3, triangle.Height);
        Assert.Equal(3, triangle.GetVoxelSet().Count()); // 1-cell footprint × 3 height (boundary excluded)
    }

    [Fact]
    public void Polygon_RoundTrips()
    {
        var polygon = RoundTrip(new Polygon(new[] { new Int2(0, 0), new Int2(2, 0), new Int2(2, 2), new Int2(0, 2) }, 2));
        Assert.Equal(2, polygon.Height);
        Assert.Equal(8, polygon.GetVoxelSet().Count()); // 4-cell footprint × 2 height
    }

    [Fact]
    public void Circle_RoundTrips()
    {
        var circle = RoundTrip(new Circle(3, 4));
        Assert.Equal(4, circle.Height);
        Assert.NotEmpty(circle.SectionCells); // 填充圆截面已持久化
        Assert.All(circle.SectionCells, c => Assert.True(c.X * c.X + c.Z * c.Z <= 9));
    }

    [Fact]
    public void Cylinder_RoundTripsAsCylinder()
    {
        var cylinder = RoundTrip(new Cylinder(3, 4));
        Assert.IsType<Cylinder>(cylinder);
        Assert.Equal(4, cylinder.Height);
    }

    [Fact]
    public void Ellipse_RoundTrips()
    {
        var ellipse = RoundTrip(new Ellipse(2, 3, 4));
        Assert.Equal(4, ellipse.Height);
    }

    [Fact]
    public void Ring_RoundTrips()
    {
        var ring = RoundTrip(new Ring(1, 3, 4));
        Assert.Equal(4, ring.Height);
    }

    [Fact]
    public void Cone_RoundTrips()
    {
        var cone = RoundTrip(new Cone(2, 5));
        Assert.Equal(2, cone.Radius);
        Assert.Equal(5, cone.Height);
    }

    [Fact]
    public void Pyramid_RoundTrips()
    {
        var pyramid = RoundTrip(new Pyramid(2, 3, 4));
        Assert.Equal(2, pyramid.SizeX);
        Assert.Equal(3, pyramid.SizeZ);
        Assert.Equal(4, pyramid.Height);
    }

    [Fact]
    public void Sphere_RoundTrips()
    {
        var sphere = RoundTrip(new Sphere(3));
        Assert.Equal(3, sphere.Radius);
    }

    [Fact]
    public void Ellipsoid_RoundTrips()
    {
        var ellipsoid = RoundTrip(new Ellipsoid(2, 3, 4));
        Assert.Equal(2, ellipsoid.RadiusX);
        Assert.Equal(3, ellipsoid.RadiusY);
        Assert.Equal(4, ellipsoid.RadiusZ);
    }

    [Fact]
    public void Extruded_RoundTripsWithSectionCells()
    {
        var extruded = RoundTrip(new Extruded(new[]
        {
            new Int2(0, 0), new Int2(1, 0), new Int2(2, 0),
            new Int2(0, 1), new Int2(1, 1), new Int2(2, 1),
        }, 4)); // 2×3 截面
        Assert.Equal(4, extruded.Height);
        Assert.Equal(6, extruded.SectionCells.Count);
        Assert.Equal(2 * 3 * 4, extruded.GetVoxelSet().Count());
    }

    [Fact]
    public void Extruded_DefaultsToEmptySection()
    {
        var extruded = RoundTrip(new Extruded());
        Assert.Empty(extruded.SectionCells);
        Assert.Empty(extruded.GetVoxelSet());
    }

    [Fact]
    public void Composite_RoundTrips()
    {
        var composite = new Composite();
        composite.Children.Add(new CompositeChild { Shape = new Box { SizeX = 1, SizeY = 1, SizeZ = 1 }, Offset = new Int3(0, 0, 0) });
        composite.Children.Add(new CompositeChild { Shape = new Sphere(1), Offset = new Int3(2, 0, 0) });

        var result = RoundTrip(composite);
        Assert.Equal(2, result.Children.Count);
        Assert.IsType<Box>(result.Children[0].Shape);
        Assert.IsType<Sphere>(result.Children[1].Shape);
        Assert.Equal(new Int3(2, 0, 0), result.Children[1].Offset);
    }

    [Fact]
    public void Box_SerializesToFlatKindJson()
    {
        var json = JsonConvert.SerializeObject(new Box { SizeX = 1, SizeY = 2, SizeZ = 3 }, Settings);
        Assert.Equal("""{"kind":"box","size_x":1,"size_y":2,"size_z":3}""", json);
    }

    [Fact]
    public void Composite_SerializesNestedVolumesWithKind()
    {
        var composite = new Composite();
        composite.Children.Add(new CompositeChild { Shape = new Box { SizeX = 1, SizeY = 1, SizeZ = 1 }, Offset = new Int3(0, 0, 0) });

        var json = JsonConvert.SerializeObject(composite, Settings);
        Assert.Contains("\"kind\":\"box\"", json);
        Assert.Contains("\"offset\":{\"x\":0,\"y\":0,\"z\":0}", json);
    }
}
