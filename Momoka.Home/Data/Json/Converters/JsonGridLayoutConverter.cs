using Momoka.Home.Layouts;
using Momoka.Home.Primitives;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
namespace Momoka.Home.Data.Json.Converters;

/// <summary>
/// Serializes a <see cref="GridLayout{T}"/> of bools: size, unit length and
/// the cell array (row-major over XZ). Attached via [JsonConverter] on
/// <see cref="Momoka.Home.Entities.PlacementLayoutSource.Layout"/> so nested component
/// serialization resolves it. 姿态（Transform）随组件成员序列化，不在此。
/// </summary>
public class JsonGridLayoutConverter : JsonConverter<GridLayout<bool>>
{
    public override void WriteJson(JsonWriter writer, GridLayout<bool>? value, JsonSerializer serializer)
    {
        if (value is null)
        {
            writer.WriteNull();
            return;
        }

        writer.WriteStartObject();
        writer.WritePropertyName("size");
        WriteInt2(writer, value.Size);
        writer.WritePropertyName("unit_length");
        writer.WriteValue(value.UnitLength);
        writer.WritePropertyName("cells");
        writer.WriteStartArray();
        for (var z = 0; z < value.Size.Z; z++)
            for (var x = 0; x < value.Size.X; x++)
                writer.WriteValue(value[new Int2(x, z)]);
        writer.WriteEndArray();
        writer.WriteEndObject();
    }

    public override GridLayout<bool>? ReadJson(JsonReader reader, Type objectType, GridLayout<bool>? existingValue, bool hasExistingValue, JsonSerializer serializer)
    {
        var obj = JObject.Load(reader);
        var size = ReadInt2(obj["size"]);
        var unitLength = obj["unit_length"]?.Value<float>() ?? 10f;
        var cells = obj["cells"]!.ToObject<bool[]>();

        var grid = new GridLayout<bool>(size) { UnitLength = unitLength };
        var i = 0;
        for (var z = 0; z < size.Z; z++)
            for (var x = 0; x < size.X; x++)
                grid[new Int2(x, z)] = cells![i++];
        return grid;
    }

    private static void WriteInt2(JsonWriter writer, Int2 value)
    {
        writer.WriteStartArray();
        writer.WriteValue(value.X);
        writer.WriteValue(value.Z);
        writer.WriteEndArray();
    }

    private static Int2 ReadInt2(JToken? token) =>
        new(token![0]!.Value<int>(), token![1]!.Value<int>());
}
