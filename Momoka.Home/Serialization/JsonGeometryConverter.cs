using Momoka.Home.Geometry;
using Momoka.Home.Primitives;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
namespace Momoka.Home.Serialization;

/// <summary>
/// Serializes a <see cref="Volume"/> (and the 2D <see cref="Shape"/> footprints it
/// embeds) to/from its declarative JSON form: the "kind" discriminator declared
/// via <see cref="JsonTypeNameAttribute"/> plus kind-specific params. Dispatch is
/// registry/codec-driven (exact runtime type → writer, kind → reader) instead of
/// one large switch, so adding a shape means one attribute plus one small codec
/// entry. Replaces the old JsonVoxelShapeConverter + ShapeDto/ShapeFactory.
/// </summary>
public class JsonGeometryConverter : JsonConverter<Volume>
{
    private static readonly Dictionary<Type, Action<JsonWriter, Volume>> VolumeWriters = new();
    private static readonly Dictionary<string, Func<JObject, Volume>> VolumeReaders = new();
    private static readonly Dictionary<Type, Action<JsonWriter, Shape>> ShapeWriters = new();
    private static readonly Dictionary<string, Func<JObject, Shape>> ShapeReaders = new();

    static JsonGeometryConverter()
    {
        // ── 3D volumes ────────────────────────────────────

        RegisterVolume<Box3D>(
            (w, b) => WriteInt3(w, "size", new Int3(b.SizeX, b.SizeY, b.SizeZ)),
            o => new Box3D
            {
                SizeX = Axis(o, "size", "x", 1),
                SizeY = Axis(o, "size", "y", 1),
                SizeZ = Axis(o, "size", "z", 1)
            });

        RegisterVolume<Line3D>(
            (w, l) =>
            {
                WriteVec3(w, "start", l.Start);
                WriteVec3(w, "end", l.End);
                WriteInt(w, "thickness", l.Thickness);
            },
            o => new Line3D
            {
                Start = ReadVec3(o["start"]),
                End = ReadVec3(o["end"]),
                Thickness = ReadInt(o, "thickness", 1)
            });

        RegisterVolume<Curve3D>(
            (w, c) =>
            {
                WriteVec3(w, "start", c.Start);
                WriteVec3(w, "end", c.End);
                WriteFloat(w, "curvature", c.Curvature);
                WriteInt(w, "thickness", c.Thickness);
            },
            o => new Curve3D
            {
                Start = ReadVec3(o["start"]),
                End = ReadVec3(o["end"]),
                Curvature = ReadFloat(o, "curvature", 0f),
                Thickness = ReadInt(o, "thickness", 1)
            });

        RegisterVolume<Triangle3D>(
            (w, t) =>
            {
                WriteTrianglePoints(w, t);
                WriteInt(w, "height", t.Height);
            },
            o => new Triangle3D(ReadInt2(o["a"]), ReadInt2(o["b"]), ReadInt2(o["c"]), ReadInt(o, "height", 1)));

        RegisterVolume<Polygon3D>(
            (w, p) =>
            {
                if (p.Footprint is Polygon2D footprint)
                    WriteVertices(w, footprint);
                WriteInt(w, "height", p.Height);
            },
            o => new Polygon3D(ReadVertices(o["vertices"]), ReadInt(o, "height", 1)));

        RegisterVolume<Circle3D>(
            (w, c) =>
            {
                WriteInt(w, "radius", RadiusOf(c));
                WriteInt(w, "height", c.Height);
            },
            o => new Circle3D(ReadInt(o, "radius", 1), ReadInt(o, "height", 1)));

        RegisterVolume<Cylinder3D>(
            (w, c) =>
            {
                WriteInt(w, "radius", RadiusOf(c));
                WriteInt(w, "height", c.Height);
            },
            o => new Cylinder3D(ReadInt(o, "radius", 1), ReadInt(o, "height", 1)));

        RegisterVolume<Ellipse3D>(
            (w, e) =>
            {
                WriteInt(w, "radius_x", e.Footprint is Ellipse2D ex ? ex.RadiusX : 1);
                WriteInt(w, "radius_z", e.Footprint is Ellipse2D ez ? ez.RadiusZ : 1);
                WriteInt(w, "height", e.Height);
            },
            o => new Ellipse3D(ReadInt(o, "radius_x", 1), ReadInt(o, "radius_z", 1), ReadInt(o, "height", 1)));

        RegisterVolume<Ring3D>(
            (w, r) =>
            {
                WriteInt(w, "inner_radius", r.Footprint is Ring2D ri ? ri.InnerRadius : 1);
                WriteInt(w, "outer_radius", r.Footprint is Ring2D ro ? ro.OuterRadius : 2);
                WriteInt(w, "height", r.Height);
            },
            o => new Ring3D(ReadInt(o, "inner_radius", 1), ReadInt(o, "outer_radius", 2), ReadInt(o, "height", 1)));

        RegisterVolume<Cone3D>(
            (w, c) =>
            {
                WriteInt(w, "radius", c.Radius);
                WriteInt(w, "height", c.Height);
            },
            o => new Cone3D(ReadInt(o, "radius", 1), ReadInt(o, "height", 1)));

        RegisterVolume<Pyramid3D>(
            (w, p) =>
            {
                WriteInt(w, "size_x", p.SizeX);
                WriteInt(w, "size_z", p.SizeZ);
                WriteInt(w, "height", p.Height);
            },
            o => new Pyramid3D(ReadInt(o, "size_x", 1), ReadInt(o, "size_z", 1), ReadInt(o, "height", 1)));

        RegisterVolume<Sphere3D>(
            (w, s) => WriteInt(w, "radius", s.Radius),
            o => new Sphere3D(ReadInt(o, "radius", 1)));

        RegisterVolume<Ellipsoid3D>(
            (w, e) =>
            {
                WriteInt(w, "radius_x", e.RadiusX);
                WriteInt(w, "radius_y", e.RadiusY);
                WriteInt(w, "radius_z", e.RadiusZ);
            },
            o => new Ellipsoid3D(ReadInt(o, "radius_x", 1), ReadInt(o, "radius_y", 1), ReadInt(o, "radius_z", 1)));

        RegisterVolume<Extruded3D>(
            (w, e) =>
            {
                w.WritePropertyName("footprint");
                WriteShape(w, e.Footprint);
                WriteInt(w, "height", e.Height);
            },
            o => new Extruded3D(ReadShape(o["footprint"]), ReadInt(o, "height", 1)));

        RegisterVolume<Composite3D>(
            (w, c) =>
            {
                w.WritePropertyName("children");
                w.WriteStartArray();
                foreach (var (volume, offset) in c.Children)
                {
                    w.WriteStartObject();
                    w.WritePropertyName("shape");
                    WriteVolume(w, volume);
                    w.WritePropertyName("offset");
                    WriteInt3Inline(w, offset);
                    w.WriteEndObject();
                }
                w.WriteEndArray();
            },
            o => ReadComposite(o));

        // ── 2D footprints ────────────────────────────────

        RegisterShape<Rect2D>(
            (w, r) =>
            {
                WriteInt(w, "size_x", r.SizeX);
                WriteInt(w, "size_z", r.SizeZ);
            },
            o => new Rect2D(ReadInt(o, "size_x", 1), ReadInt(o, "size_z", 1)));

        RegisterShape<Polygon2D>(
            (w, p) => WriteVertices(w, p),
            o => new Polygon2D(ReadVertices(o["vertices"])));

        RegisterShape<Circle2D>(
            (w, c) => WriteInt(w, "radius", c.Radius),
            o => new Circle2D(ReadInt(o, "radius", 1)));

        RegisterShape<Ellipse2D>(
            (w, e) =>
            {
                WriteInt(w, "radius_x", e.RadiusX);
                WriteInt(w, "radius_z", e.RadiusZ);
            },
            o => new Ellipse2D(ReadInt(o, "radius_x", 1), ReadInt(o, "radius_z", 1)));

        RegisterShape<Ring2D>(
            (w, r) =>
            {
                WriteInt(w, "inner_radius", r.InnerRadius);
                WriteInt(w, "outer_radius", r.OuterRadius);
            },
            o => new Ring2D(ReadInt(o, "inner_radius", 1), ReadInt(o, "outer_radius", 2)));

        RegisterShape<Composite2D>(
            (w, c) =>
            {
                w.WritePropertyName("children");
                w.WriteStartArray();
                foreach (var (shape, offset) in c.Children)
                {
                    w.WriteStartObject();
                    w.WritePropertyName("shape");
                    WriteShape(w, shape);
                    w.WritePropertyName("offset");
                    WritePointInline(w, offset);
                    w.WriteEndObject();
                }
                w.WriteEndArray();
            },
            o => ReadComposite2D(o));
    }

    public override Volume? ReadJson(JsonReader reader, Type objectType, Volume? existingValue, bool hasExistingValue, JsonSerializer serializer)
        => ReadVolume(JObject.Load(reader));

    public override void WriteJson(JsonWriter writer, Volume? value, JsonSerializer serializer)
    {
        if (value is null)
        {
            writer.WriteNull();
            return;
        }
        WriteVolume(writer, value);
    }

    // ── Dispatch ─────────────────────────────────────────

    private static void WriteVolume(JsonWriter writer, Volume volume)
    {
        if (!VolumeWriters.TryGetValue(volume.GetType(), out var write))
            throw new NotSupportedException($"Cannot serialize volume '{volume.GetType().Name}'.");

        writer.WriteStartObject();
        writer.WritePropertyName("kind");
        writer.WriteValue(JsonTypeNameRegistry.NameOf<Volume>(volume.GetType()));
        write(writer, volume);
        writer.WriteEndObject();
    }

    private static Volume ReadVolume(JObject obj)
    {
        var kind = obj["kind"]?.Value<string>() ?? "";
        if (!VolumeReaders.TryGetValue(kind, out var read))
            throw new NotSupportedException($"Unknown volume kind '{kind}'.");
        return read(obj);
    }

    private static void WriteShape(JsonWriter writer, Shape shape)
    {
        if (!ShapeWriters.TryGetValue(shape.GetType(), out var write))
            throw new NotSupportedException($"Cannot serialize footprint '{shape.GetType().Name}'.");

        writer.WriteStartObject();
        writer.WritePropertyName("kind");
        writer.WriteValue(JsonTypeNameRegistry.NameOf<Shape>(shape.GetType()));
        write(writer, shape);
        writer.WriteEndObject();
    }

    private static Shape ReadShape(JToken? token)
    {
        if (token is not JObject obj)
            throw new InvalidDataException("Footprint is missing.");

        var kind = obj["kind"]?.Value<string>() ?? "";
        if (!ShapeReaders.TryGetValue(kind, out var read))
            throw new NotSupportedException($"Unknown footprint kind '{kind}'.");
        return read(obj);
    }

    private static void RegisterVolume<T>(Action<JsonWriter, T> write, Func<JObject, T> read) where T : Volume
    {
        var name = JsonTypeNameRegistry.NameOf<Volume>(typeof(T));
        VolumeWriters[typeof(T)] = (w, v) => write(w, (T)v);
        VolumeReaders[name] = o => read(o);
    }

    private static void RegisterShape<T>(Action<JsonWriter, T> write, Func<JObject, T> read) where T : Shape
    {
        var name = JsonTypeNameRegistry.NameOf<Shape>(typeof(T));
        ShapeWriters[typeof(T)] = (w, v) => write(w, (T)v);
        ShapeReaders[name] = o => read(o);
    }

    // ── Composite ────────────────────────────────────────

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

    private static int RadiusOf(Extruded3D e) => e.Footprint is Circle2D c ? c.Radius : 1;

    private static void WriteInt(JsonWriter writer, string name, int value)
    {
        writer.WritePropertyName(name);
        writer.WriteValue(value);
    }

    private static void WriteFloat(JsonWriter writer, string name, float value)
    {
        writer.WritePropertyName(name);
        writer.WriteValue(value);
    }

    private static void WriteVec3(JsonWriter writer, string name, Float3 v)
    {
        writer.WritePropertyName(name);
        writer.WriteStartObject();
        writer.WritePropertyName("x"); writer.WriteValue(v.X);
        writer.WritePropertyName("y"); writer.WriteValue(v.Y);
        writer.WritePropertyName("z"); writer.WriteValue(v.Z);
        writer.WriteEndObject();
    }

    private static void WriteInt3(JsonWriter writer, string name, Int3 v)
    {
        writer.WritePropertyName(name);
        writer.WriteStartObject();
        writer.WritePropertyName("x"); writer.WriteValue(v.X);
        writer.WritePropertyName("y"); writer.WriteValue(v.Y);
        writer.WritePropertyName("z"); writer.WriteValue(v.Z);
        writer.WriteEndObject();
    }

    private static void WriteInt3Inline(JsonWriter writer, Int3 v)
    {
        writer.WriteStartObject();
        writer.WritePropertyName("x"); writer.WriteValue(v.X);
        writer.WritePropertyName("y"); writer.WriteValue(v.Y);
        writer.WritePropertyName("z"); writer.WriteValue(v.Z);
        writer.WriteEndObject();
    }

    private static void WritePoint(JsonWriter writer, string name, Int2 v)
    {
        writer.WritePropertyName(name);
        writer.WriteStartObject();
        writer.WritePropertyName("x"); writer.WriteValue(v.X);
        writer.WritePropertyName("z"); writer.WriteValue(v.Z);
        writer.WriteEndObject();
    }

    private static void WritePointInline(JsonWriter writer, Int2 v)
    {
        writer.WriteStartObject();
        writer.WritePropertyName("x"); writer.WriteValue(v.X);
        writer.WritePropertyName("z"); writer.WriteValue(v.Z);
        writer.WriteEndObject();
    }

    private static void WriteVertices(JsonWriter writer, Polygon2D polygon)
    {
        writer.WritePropertyName("vertices");
        writer.WriteStartArray();
        foreach (var v in polygon.Vertices)
            WritePointInline(writer, v);
        writer.WriteEndArray();
    }

    private static void WriteTrianglePoints(JsonWriter writer, Triangle3D triangle)
    {
        if (triangle.Footprint is not Polygon2D footprint || footprint.Vertices.Count < 3)
            return;
        WritePoint(writer, "a", footprint.Vertices[0]);
        WritePoint(writer, "b", footprint.Vertices[1]);
        WritePoint(writer, "c", footprint.Vertices[2]);
    }

    // ── Read helpers ─────────────────────────────────────

    private static int ReadInt(JObject obj, string key, int fallback) =>
        obj[key] is JToken t ? t.Value<int>() : fallback;

    private static float ReadFloat(JObject obj, string key, float fallback) =>
        obj[key] is JToken t ? t.Value<float>() : fallback;

    private static int Axis(JObject obj, string group, string key, int fallback) =>
        obj[group]?[key] is JToken t ? t.Value<int>() : fallback;

    private static Float3 ReadVec3(JToken? token) =>
        token is not JObject o
            ? Float3.Zero
            : new Float3(
                o["x"]?.Value<float>() ?? 0f,
                o["y"]?.Value<float>() ?? 0f,
                o["z"]?.Value<float>() ?? 0f);

    private static Int3 ReadInt3(JToken? token) =>
        token is not JObject o
            ? Int3.Zero
            : new Int3(
                o["x"]?.Value<int>() ?? 0,
                o["y"]?.Value<int>() ?? 0,
                o["z"]?.Value<int>() ?? 0);

    private static Int2 ReadInt2(JToken? token) =>
        token is not JObject o
            ? Int2.Zero
            : new Int2(
                o["x"]?.Value<int>() ?? 0,
                o["z"]?.Value<int>() ?? 0);

    private static List<Int2> ReadVertices(JToken? token) =>
        token is not JArray array
            ? new List<Int2>()
            : array.OfType<JObject>().Select(ReadInt2).ToList();
}
