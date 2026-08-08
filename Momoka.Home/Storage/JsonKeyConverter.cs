using Momoka.Home.Primitives;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
namespace Momoka.Home.Storage;

/// <summary>Serializes <see cref="Key"/> as its <c>ns:path</c> string.</summary>
public class JsonKeyConverter : JsonConverter<Key>
{
    public override void WriteJson(JsonWriter writer, Key value, JsonSerializer serializer) =>
        writer.WriteValue(value.ToString());

    public override Key ReadJson(JsonReader reader, Type objectType, Key existingValue, bool hasExistingValue, JsonSerializer serializer)
    {
        var token = JToken.Load(reader);
        if (token.Type != JTokenType.String)
            throw new JsonSerializationException("Key must be a string.");
        return Key.Parse(token.Value<string>()!);
    }
}
