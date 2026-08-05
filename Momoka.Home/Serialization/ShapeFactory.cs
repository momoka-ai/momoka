using Momoka.Home.Primitives;
using Momoka.Home.Shapes;
using Newtonsoft.Json.Linq;
namespace Momoka.Home.Serialization;

/// <summary>
/// Builds a <see cref="Shape"/> from its declarative <see cref="ShapeDto"/>. The
/// "kind" string selects the shape class — the single place geometry meets config.
/// Shapes themselves stay pure geometry with no serialization knowledge.
/// </summary>
public static class ShapeFactory
{
    public static Shape Create(ShapeDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);
        return dto.Kind switch
        {
            "box" => CreateBox(dto),
            "line" => CreateLine(dto),
            "curve" => CreateCurve(dto),
            _ => throw new NotSupportedException($"Unknown shape kind '{dto.Kind}'.")
        };
    }

    private static BoxShape CreateBox(ShapeDto dto)
    {
        var size = dto.Params.TryGetValue("size", out var token) ? token as JObject : null;
        return new BoxShape
        {
            SizeX = GetAxis(size, "x", 1),
            SizeY = GetAxis(size, "y", 1),
            SizeZ = GetAxis(size, "z", 1)
        };
    }

    private static LineShape CreateLine(ShapeDto dto)
    {
        return new LineShape
        {
            Start = ReadVec3(dto.Params, "start"),
            End = ReadVec3(dto.Params, "end"),
            Thickness = ReadInt(dto.Params, "thickness", 1)
        };
    }

    private static CurveShape CreateCurve(ShapeDto dto)
    {
        return new CurveShape
        {
            Start = ReadVec3(dto.Params, "start"),
            End = ReadVec3(dto.Params, "end"),
            Thickness = ReadInt(dto.Params, "thickness", 1),
            Curvature = ReadFloat(dto.Params, "curvature", 0f)
        };
    }

    private static int GetAxis(JObject? size, string axis, int fallback) =>
        size is not null && size[axis] is JToken t ? t.Value<int>() : fallback;

    private static int ReadInt(Dictionary<string, JToken> p, string key, int fallback) =>
        p.TryGetValue(key, out var t) ? t.Value<int>() : fallback;

    private static float ReadFloat(Dictionary<string, JToken> p, string key, float fallback) =>
        p.TryGetValue(key, out var t) ? t.Value<float>() : fallback;

    private static Float3 ReadVec3(Dictionary<string, JToken> p, string key)
    {
        if (p.TryGetValue(key, out var t) && t is JObject o)
        {
            return new Float3(
                o["x"]?.Value<float>() ?? 0f,
                o["y"]?.Value<float>() ?? 0f,
                o["z"]?.Value<float>() ?? 0f);
        }
        return Float3.Zero;
    }
}
