using Momoka.Home.Primitives;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
namespace Momoka.Home.Storage;

/// <summary>Serializes <see cref="Int2"/> compactly as <c>[x, z]</c>.</summary>
public class JsonInt2Converter : JsonConverter<Int2>
{
    public override void WriteJson(JsonWriter writer, Int2 value, JsonSerializer serializer)
    {
        writer.WriteStartArray();
        writer.WriteValue(value.X);
        writer.WriteValue(value.Z);
        writer.WriteEndArray();
    }

    public override Int2 ReadJson(JsonReader reader, Type objectType, Int2 existingValue, bool hasExistingValue, JsonSerializer serializer)
    {
        var arr = JArray.Load(reader);
        return new Int2(arr[0].Value<int>(), arr[1].Value<int>());
    }
}
