using Momoka.Home.Geometry;
using Momoka.Home.Primitives;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
namespace Momoka.Home.Serialization;

/// <summary>
/// Serializes a <see cref="Volume"/> to/from its declarative JSON form: the
/// "kind" discriminator plus kind-specific params. The single place geometry
/// meets config. Replaces the old ShapeDto + ShapeFactory.
/// </summary>
public class JsonVoxelShapeConverter : JsonConverter<Volume>
{
    public override Volume? ReadJson(JsonReader reader, Type objectType, Volume? existingValue, bool hasExistingValue, JsonSerializer serializer)
    {
        var obj = JObject.Load(reader);
        return ReadVolume(obj);
    }

    public override void WriteJson(JsonWriter writer, Volume? value, JsonSerializer serializer)
    {
        if (value is null)
        {
            writer.WriteNull();
            return;
        }

        writer.WriteStartObject();
        switch (value)
        {
            case Box3D box:
                writer.WritePropertyName("kind"); writer.WriteValue("box");
                writer.WritePropertyName("size");
                WriteInt3(writer, new Int3(box.SizeX, box.SizeY, box.SizeZ));
                break;

            case Line3D line:
                writer.WritePropertyName("kind"); writer.WriteValue("line");
                writer.WritePropertyName("start"); WriteVec3(writer, line.Start);
                writer.WritePropertyName("end"); WriteVec3(writer, line.End);
                writer.WritePropertyName("thickness"); writer.WriteValue(line.Thickness);
                break;

            case Curve3D curve:
                writer.WritePropertyName("kind"); writer.WriteValue("curve");
                writer.WritePropertyName("start"); WriteVec3(writer, curve.Start);
                writer.WritePropertyName("end"); WriteVec3(writer, curve.End);
                writer.WritePropertyName("curvature"); writer.WriteValue(curve.Curvature);
                writer.WritePropertyName("thickness"); writer.WriteValue(curve.Thickness);
                break;

            case Triangle3D triangle:
                writer.WritePropertyName("kind"); writer.WriteValue("triangle");
                if (triangle.Footprint is Polygon2D tp && tp.Vertices.Count >= 3)
                {
                    writer.WritePropertyName("a"); WriteInt2(writer, tp.Vertices[0]);
                    writer.WritePropertyName("b"); WriteInt2(writer, tp.Vertices[1]);
                    writer.WritePropertyName("c"); WriteInt2(writer, tp.Vertices[2]);
                }
                writer.WritePropertyName("height"); writer.WriteValue(triangle.Height);
                break;

            case Polygon3D polygon:
                writer.WritePropertyName("kind"); writer.WriteValue("polygon");
                if (polygon.Footprint is Polygon2D pp)
                {
                    writer.WritePropertyName("vertices");
                    writer.WriteStartArray();
                    foreach (var v in pp.Vertices)
                        WriteInt2(writer, v);
                    writer.WriteEndArray();
                }
                writer.WritePropertyName("height"); writer.WriteValue(polygon.Height);
                break;

            case Cylinder3D cylinder:
                writer.WritePropertyName("kind"); writer.WriteValue("cylinder");
                writer.WritePropertyName("radius"); writer.WriteValue(cylinder.Footprint is Circle2D cyf ? cyf.Radius : 1);
                writer.WritePropertyName("height"); writer.WriteValue(cylinder.Height);
                break;

            case Circle3D circle:
                writer.WritePropertyName("kind"); writer.WriteValue("circle");
                writer.WritePropertyName("radius"); writer.WriteValue(circle.Footprint is Circle2D cf ? cf.Radius : 1);
                writer.WritePropertyName("height"); writer.WriteValue(circle.Height);
                break;

            case Ellipse3D ellipse:
                writer.WritePropertyName("kind"); writer.WriteValue("ellipse");
                if (ellipse.Footprint is Ellipse2D ef)
                {
                    writer.WritePropertyName("radius_x"); writer.WriteValue(ef.RadiusX);
                    writer.WritePropertyName("radius_z"); writer.WriteValue(ef.RadiusZ);
                }
                writer.WritePropertyName("height"); writer.WriteValue(ellipse.Height);
                break;

            case Ring3D ring:
                writer.WritePropertyName("kind"); writer.WriteValue("ring");
                if (ring.Footprint is Ring2D rf)
                {
                    writer.WritePropertyName("inner_radius"); writer.WriteValue(rf.InnerRadius);
                    writer.WritePropertyName("outer_radius"); writer.WriteValue(rf.OuterRadius);
                }
                writer.WritePropertyName("height"); writer.WriteValue(ring.Height);
                break;

            case Cone3D cone:
                writer.WritePropertyName("kind"); writer.WriteValue("cone");
                writer.WritePropertyName("radius"); writer.WriteValue(cone.Radius);
                writer.WritePropertyName("height"); writer.WriteValue(cone.Height);
                break;

            case Pyramid3D pyramid:
                writer.WritePropertyName("kind"); writer.WriteValue("pyramid");
                writer.WritePropertyName("size_x"); writer.WriteValue(pyramid.SizeX);
                writer.WritePropertyName("size_z"); writer.WriteValue(pyramid.SizeZ);
                writer.WritePropertyName("height"); writer.WriteValue(pyramid.Height);
                break;

            case Sphere3D sphere:
                writer.WritePropertyName("kind"); writer.WriteValue("sphere");
                writer.WritePropertyName("radius"); writer.WriteValue(sphere.Radius);
                break;

            case Ellipsoid3D ellipsoid:
                writer.WritePropertyName("kind"); writer.WriteValue("ellipsoid");
                writer.WritePropertyName("radius_x"); writer.WriteValue(ellipsoid.RadiusX);
                writer.WritePropertyName("radius_y"); writer.WriteValue(ellipsoid.RadiusY);
                writer.WritePropertyName("radius_z"); writer.WriteValue(ellipsoid.RadiusZ);
                break;

            case Extruded3D extruded:
                writer.WritePropertyName("kind"); writer.WriteValue("extruded");
                writer.WritePropertyName("footprint"); WriteShape(writer, extruded.Footprint);
                writer.WritePropertyName("height"); writer.WriteValue(extruded.Height);
                break;

            case Composite3D composite:
                writer.WritePropertyName("kind"); writer.WriteValue("composite");
                writer.WritePropertyName("children");
                writer.WriteStartArray();
                foreach (var (shape, offset) in composite.Children)
                {
                    writer.WriteStartObject();
                    writer.WritePropertyName("shape");
                    // Recursion: reuses this converter (registered in the factory's settings).
                    JsonSerializer.CreateDefault(new JsonSerializerSettings { Converters = { new JsonVoxelShapeConverter() } }).Serialize(writer, shape);
                    writer.WritePropertyName("offset"); WriteInt3(writer, offset);
                    writer.WriteEndObject();
                }
                writer.WriteEndArray();
                break;

            default:
                throw new NotSupportedException($"Cannot serialize shape '{value.GetType().Name}'.");
        }
        writer.WriteEndObject();
    }

    // ── Read ─────────────────────────────────────────────

    private static Volume ReadVolume(JObject obj)
    {
        var kind = obj["kind"]?.Value<string>() ?? "";
        return kind switch
        {
            "box" => new Box3D
            {
                SizeX = Axis(obj, "size", "x", 1),
                SizeY = Axis(obj, "size", "y", 1),
                SizeZ = Axis(obj, "size", "z", 1)
            },
            "line" => new Line3D
            {
                Start = ReadVec3(obj["start"]),
                End = ReadVec3(obj["end"]),
                Thickness = ReadInt(obj, "thickness", 1)
            },
            "curve" => new Curve3D
            {
                Start = ReadVec3(obj["start"]),
                End = ReadVec3(obj["end"]),
                Curvature = ReadFloat(obj, "curvature", 0f),
                Thickness = ReadInt(obj, "thickness", 1)
            },
            "polygon" => new Polygon3D(ReadVertices(obj["vertices"]), ReadInt(obj, "height", 1)),
            "triangle" => new Triangle3D(ReadInt2(obj["a"]), ReadInt2(obj["b"]), ReadInt2(obj["c"]), ReadInt(obj, "height", 1)),
            "circle" => new Circle3D(ReadInt(obj, "radius", 1), ReadInt(obj, "height", 1)),
            "ellipse" => new Ellipse3D(ReadInt(obj, "radius_x", 1), ReadInt(obj, "radius_z", 1), ReadInt(obj, "height", 1)),
            "ring" => new Ring3D(ReadInt(obj, "inner_radius", 1), ReadInt(obj, "outer_radius", 2), ReadInt(obj, "height", 1)),
            "cylinder" => new Cylinder3D(ReadInt(obj, "radius", 1), ReadInt(obj, "height", 1)),
            "cone" => new Cone3D(ReadInt(obj, "radius", 1), ReadInt(obj, "height", 1)),
            "pyramid" => new Pyramid3D(ReadInt(obj, "size_x", 1), ReadInt(obj, "size_z", 1), ReadInt(obj, "height", 1)),
            "sphere" => new Sphere3D(ReadInt(obj, "radius", 1)),
            "ellipsoid" => new Ellipsoid3D(ReadInt(obj, "radius_x", 1), ReadInt(obj, "radius_y", 1), ReadInt(obj, "radius_z", 1)),
            "extruded" => new Extruded3D(ReadShape(obj["footprint"]), ReadInt(obj, "height", 1)),
            "composite" => ReadComposite(obj),
            _ => throw new NotSupportedException($"Unknown shape kind '{kind}'.")
        };
    }

    private static Composite3D ReadComposite(JObject obj)
    {
        var composite = new Composite3D();
        if (obj["children"] is JArray children)
        {
            foreach (var child in children.OfType<JObject>())
            {
                var volume = ReadVolume(child["shape"] is JObject s ? s : new JObject());
                composite.Children.Add((volume, ReadInt3(child["offset"])));
            }
        }
        return composite;
    }

    private static Shape ReadShape(JToken? token)
    {
        if (token is not JObject obj)
            throw new InvalidDataException("Extruded footprint is missing.");

        var kind = obj["kind"]?.Value<string>() ?? "";
        return kind switch
        {
            "rect" => new Rect2D(ReadInt(obj, "size_x", 1), ReadInt(obj, "size_z", 1)),
            "polygon" => new Polygon2D(ReadVertices(obj["vertices"])),
            "circle" => new Circle2D(ReadInt(obj, "radius", 1)),
            "ellipse" => new Ellipse2D(ReadInt(obj, "radius_x", 1), ReadInt(obj, "radius_z", 1)),
            "ring" => new Ring2D(ReadInt(obj, "inner_radius", 1), ReadInt(obj, "outer_radius", 2)),
            "composite" => ReadComposite2D(obj),
            _ => throw new NotSupportedException($"Unknown footprint kind '{kind}'.")
        };
    }

    private static Composite2D ReadComposite2D(JObject obj)
    {
        var composite = new Composite2D();
        if (obj["children"] is JArray children)
        {
            foreach (var child in children.OfType<JObject>())
            {
                var shape = ReadShape(child["shape"]);
                composite.Children.Add((shape, ReadInt2(child["offset"])));
            }
        }
        return composite;
    }

    // ── Write helpers ────────────────────────────────────

    private static void WriteShape(JsonWriter writer, Shape shape)
    {
        writer.WriteStartObject();
        switch (shape)
        {
            case Rect2D rect:
                writer.WritePropertyName("kind"); writer.WriteValue("rect");
                writer.WritePropertyName("size_x"); writer.WriteValue(rect.SizeX);
                writer.WritePropertyName("size_z"); writer.WriteValue(rect.SizeZ);
                break;

            case Polygon2D polygon:
                writer.WritePropertyName("kind"); writer.WriteValue("polygon");
                writer.WritePropertyName("vertices");
                writer.WriteStartArray();
                foreach (var v in polygon.Vertices)
                    WriteInt2(writer, v);
                writer.WriteEndArray();
                break;

            case Circle2D circle:
                writer.WritePropertyName("kind"); writer.WriteValue("circle");
                writer.WritePropertyName("radius"); writer.WriteValue(circle.Radius);
                break;

            case Ellipse2D ellipse:
                writer.WritePropertyName("kind"); writer.WriteValue("ellipse");
                writer.WritePropertyName("radius_x"); writer.WriteValue(ellipse.RadiusX);
                writer.WritePropertyName("radius_z"); writer.WriteValue(ellipse.RadiusZ);
                break;

            case Ring2D ring:
                writer.WritePropertyName("kind"); writer.WriteValue("ring");
                writer.WritePropertyName("inner_radius"); writer.WriteValue(ring.InnerRadius);
                writer.WritePropertyName("outer_radius"); writer.WriteValue(ring.OuterRadius);
                break;

            case Composite2D composite:
                writer.WritePropertyName("kind"); writer.WriteValue("composite");
                writer.WritePropertyName("children");
                writer.WriteStartArray();
                foreach (var (child, offset) in composite.Children)
                {
                    writer.WriteStartObject();
                    writer.WritePropertyName("shape"); WriteShape(writer, child);
                    writer.WritePropertyName("offset"); WriteInt2(writer, offset);
                    writer.WriteEndObject();
                }
                writer.WriteEndArray();
                break;

            default:
                throw new NotSupportedException($"Cannot serialize footprint '{shape.GetType().Name}'.");
        }
        writer.WriteEndObject();
    }

    private static void WriteVec3(JsonWriter writer, Float3 v)
    {
        writer.WriteStartObject();
        writer.WritePropertyName("x"); writer.WriteValue(v.X);
        writer.WritePropertyName("y"); writer.WriteValue(v.Y);
        writer.WritePropertyName("z"); writer.WriteValue(v.Z);
        writer.WriteEndObject();
    }

    private static void WriteInt3(JsonWriter writer, Int3 v)
    {
        writer.WriteStartObject();
        writer.WritePropertyName("x"); writer.WriteValue(v.X);
        writer.WritePropertyName("y"); writer.WriteValue(v.Y);
        writer.WritePropertyName("z"); writer.WriteValue(v.Z);
        writer.WriteEndObject();
    }

    private static void WriteInt2(JsonWriter writer, Int2 v)
    {
        writer.WriteStartObject();
        writer.WritePropertyName("x"); writer.WriteValue(v.X);
        writer.WritePropertyName("z"); writer.WriteValue(v.Z);
        writer.WriteEndObject();
    }

    // ── Read helpers ─────────────────────────────────────

    private static int ReadInt(JObject obj, string key, int fallback) =>
        obj[key] is JToken t ? t.Value<int>() : fallback;

    private static float ReadFloat(JObject obj, string key, float fallback) =>
        obj[key] is JToken t ? t.Value<float>() : fallback;

    private static int Axis(JObject obj, string group, string key, int fallback) =>
        obj[group] is JObject g && g[key] is JToken t ? t.Value<int>() : fallback;

    private static Int2 ReadInt2(JToken? token) =>
        token is JObject o
            ? new Int2(o["x"]?.Value<int>() ?? 0, o["z"]?.Value<int>() ?? 0)
            : default;

    private static Int3 ReadInt3(JToken? token) =>
        token is JObject o
            ? new Int3(o["x"]?.Value<int>() ?? 0, o["y"]?.Value<int>() ?? 0, o["z"]?.Value<int>() ?? 0)
            : default;

    private static Float3 ReadVec3(JToken? token) =>
        token is JObject o
            ? new Float3(o["x"]?.Value<float>() ?? 0f, o["y"]?.Value<float>() ?? 0f, o["z"]?.Value<float>() ?? 0f)
            : Float3.Zero;

    private static List<Int2> ReadVertices(JToken? token)
    {
        var result = new List<Int2>();
        if (token is JArray array)
        {
            foreach (var item in array.OfType<JObject>())
                result.Add(ReadInt2(item));
        }
        return result;
    }
}
