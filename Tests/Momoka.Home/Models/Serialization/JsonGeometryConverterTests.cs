using Xunit;
using Momoka.Home.Geometry;
using Momoka.Home.Primitives;
using Momoka.Home.Storage;
using Newtonsoft.Json;
namespace Momoka.Home.Tests.Models.Serialization;

/// <summary>
/// Round-trips every <see cref="Volume"/> kind (and the 2D <see cref="Shape"/>
/// footprints they embed) through <see cref="JsonGeometryConverter"/>. Locks the
/// declarative JSON format so the registry/codec rewrite stays format-compatible.
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
        var box = RoundTrip(new Box3D { SizeX = 1, SizeY = 2, SizeZ = 3 });
        Assert.Equal(1, box.SizeX);
        Assert.Equal(2, box.SizeY);
        Assert.Equal(3, box.SizeZ);
    }

    [Fact]
    public void Line_RoundTrips()
    {
        var line = RoundTrip(new Line3D { Start = new Float3(1, 2, 3), End = new Float3(6, 2, 0), Thickness = 2 });
        Assert.Equal(new Float3(1, 2, 3), line.Start);
        Assert.Equal(new Float3(6, 2, 0), line.End);
        Assert.Equal(2, line.Thickness);
    }

    [Fact]
    public void Curve_RoundTrips()
    {
        var curve = RoundTrip(new Curve3D { Start = new Float3(0, 0, 0), End = new Float3(6, 0, 0), Curvature = 2, Thickness = 1 });
        Assert.Equal(2, curve.Curvature);
        Assert.Equal(6, curve.End.X);
    }

    [Fact]
    public void Triangle_RoundTrips()
    {
        var triangle = RoundTrip(new Triangle3D(new Int2(0, 0), new Int2(2, 0), new Int2(0, 2), 3));
        Assert.Equal(3, triangle.Height);
        Assert.Equal(3, triangle.Cells3D().Count()); // 1-cell footprint × 3 height (boundary excluded)
    }

    [Fact]
    public void Polygon_RoundTrips()
    {
        var polygon = RoundTrip(new Polygon3D(new[] { new Int2(0, 0), new Int2(2, 0), new Int2(2, 2), new Int2(0, 2) }, 2));
        Assert.Equal(2, polygon.Height);
        Assert.Equal(8, polygon.Cells3D().Count()); // 4-cell footprint × 2 height
    }

    [Fact]
    public void Circle_RoundTrips()
    {
        var circle = RoundTrip(new Circle3D(3, 4));
        Assert.Equal(4, circle.Height);
        var footprint = Assert.IsType<Circle2D>(circle.Footprint);
        Assert.Equal(3, footprint.Radius);
    }

    [Fact]
    public void Cylinder_RoundTripsAsCylinder()
    {
        var cylinder = RoundTrip(new Cylinder3D(3, 4));
        Assert.IsType<Cylinder3D>(cylinder);
        Assert.Equal(4, cylinder.Height);
    }

    [Fact]
    public void Ellipse_RoundTrips()
    {
        var ellipse = RoundTrip(new Ellipse3D(2, 3, 4));
        Assert.Equal(4, ellipse.Height);
    }

    [Fact]
    public void Ring_RoundTrips()
    {
        var ring = RoundTrip(new Ring3D(1, 3, 4));
        Assert.Equal(4, ring.Height);
    }

    [Fact]
    public void Cone_RoundTrips()
    {
        var cone = RoundTrip(new Cone3D(2, 5));
        Assert.Equal(2, cone.Radius);
        Assert.Equal(5, cone.Height);
    }

    [Fact]
    public void Pyramid_RoundTrips()
    {
        var pyramid = RoundTrip(new Pyramid3D(2, 3, 4));
        Assert.Equal(2, pyramid.SizeX);
        Assert.Equal(3, pyramid.SizeZ);
        Assert.Equal(4, pyramid.Height);
    }

    [Fact]
    public void Sphere_RoundTrips()
    {
        var sphere = RoundTrip(new Sphere3D(3));
        Assert.Equal(3, sphere.Radius);
    }

    [Fact]
    public void Ellipsoid_RoundTrips()
    {
        var ellipsoid = RoundTrip(new Ellipsoid3D(2, 3, 4));
        Assert.Equal(2, ellipsoid.RadiusX);
        Assert.Equal(3, ellipsoid.RadiusY);
        Assert.Equal(4, ellipsoid.RadiusZ);
    }

    [Fact]
    public void Extruded_RoundTripsWithRectFootprint()
    {
        var extruded = RoundTrip(new Extruded3D(new Rect2D(2, 3), 4));
        Assert.Equal(4, extruded.Height);
        var footprint = Assert.IsType<Rect2D>(extruded.Footprint);
        Assert.Equal(2, footprint.SizeX);
        Assert.Equal(3, footprint.SizeZ);
    }

    [Fact]
    public void Extruded_RoundTripsWithCompositeFootprint()
    {
        var composite = new Composite2D();
        composite.Children.Add(new CompositeChild2D { Shape = new Rect2D(2, 1), Offset = new Int2(0, 0) });
        composite.Children.Add(new CompositeChild2D { Shape = new Circle2D(1), Offset = new Int2(3, 0) });

        var extruded = RoundTrip(new Extruded3D(composite, 2));
        var footprint = Assert.IsType<Composite2D>(extruded.Footprint);
        Assert.Equal(2, footprint.Children.Count);
    }

    [Fact]
    public void Composite_RoundTrips()
    {
        var composite = new Composite3D();
        composite.Children.Add(new CompositeChild3D { Shape = new Box3D { SizeX = 1, SizeY = 1, SizeZ = 1 }, Offset = new Int3(0, 0, 0) });
        composite.Children.Add(new CompositeChild3D { Shape = new Sphere3D(1), Offset = new Int3(2, 0, 0) });

        var result = RoundTrip(composite);
        Assert.Equal(2, result.Children.Count);
        Assert.IsType<Box3D>(result.Children[0].Shape);
        Assert.IsType<Sphere3D>(result.Children[1].Shape);
        Assert.Equal(new Int3(2, 0, 0), result.Children[1].Offset);
    }

    [Fact]
    public void Box_SerializesToFlatKindJson()
    {
        var json = JsonConvert.SerializeObject(new Box3D { SizeX = 1, SizeY = 2, SizeZ = 3 }, Settings);
        Assert.Equal("""{"kind":"box","size_x":1,"size_y":2,"size_z":3}""", json);
    }

    [Fact]
    public void Composite_SerializesNestedVolumesWithKind()
    {
        var composite = new Composite3D();
        composite.Children.Add(new CompositeChild3D { Shape = new Box3D { SizeX = 1, SizeY = 1, SizeZ = 1 }, Offset = new Int3(0, 0, 0) });

        var json = JsonConvert.SerializeObject(composite, Settings);
        Assert.Contains("\"kind\":\"box\"", json);
        Assert.Contains("\"offset\":{\"x\":0,\"y\":0,\"z\":0}", json);
    }
}
