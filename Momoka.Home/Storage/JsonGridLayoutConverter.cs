using Momoka.Home.Layouts;
using Momoka.Home.Primitives;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
namespace Momoka.Home.Storage;

/// <summary>
/// Serializes a <see cref="GridLayout{T}"/> of bools: size, offset, direction
/// and the cell array (row-major over XZ). Attached via [JsonConverter] on
/// <see cref="Components.PlacementLayoutSource.Layout"/> so nested component
/// serialization resolves it.
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
        writer.WritePropertyName("offset");
        WriteInt3(writer, value.Offset);
        writer.WritePropertyName("direction");
        WriteInt3(writer, value.Direction);
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
        var offset = ReadInt3(obj["offset"]);
        var direction = ReadInt3(obj["direction"]);
        var cells = obj["cells"]!.ToObject<bool[]>();

        var grid = new GridLayout<bool>(size, offset) { Direction = direction };
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

    private static void WriteInt3(JsonWriter writer, Int3 value)
    {
        writer.WriteStartArray();
        writer.WriteValue(value.X);
        writer.WriteValue(value.Y);
        writer.WriteValue(value.Z);
        writer.WriteEndArray();
    }

    private static Int2 ReadInt2(JToken? token) =>
        new(token![0]!.Value<int>(), token![1]!.Value<int>());

    private static Int3 ReadInt3(JToken? token) =>
        new(token![0]!.Value<int>(), token![1]!.Value<int>(), token![2]!.Value<int>());
}
