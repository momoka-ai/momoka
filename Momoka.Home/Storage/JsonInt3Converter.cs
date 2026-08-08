using Momoka.Home.Primitives;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
namespace Momoka.Home.Storage;

/// <summary>Serializes <see cref="Int3"/> compactly as <c>[x, y, z]</c>.</summary>
public class JsonInt3Converter : JsonConverter<Int3>
{
    public override void WriteJson(JsonWriter writer, Int3 value, JsonSerializer serializer)
    {
        writer.WriteStartArray();
        writer.WriteValue(value.X);
        writer.WriteValue(value.Y);
        writer.WriteValue(value.Z);
        writer.WriteEndArray();
    }

    public override Int3 ReadJson(JsonReader reader, Type objectType, Int3 existingValue, bool hasExistingValue, JsonSerializer serializer)
    {
        var arr = JArray.Load(reader);
        return new Int3(arr[0].Value<int>(), arr[1].Value<int>(), arr[2].Value<int>());
    }
}
