using System.Text.Json;
using System.Text.Json.Serialization;
using Momoka.Home.Primitives;
namespace Momoka.Home.Data.Json.Converters;

/// <summary>Serializes <see cref="Key"/> as its <c>ns:path</c> string.</summary>
public class JsonKeyConverter : JsonConverter<Key>
{
    public override Key Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String)
            throw new JsonException("Key must be a string.");
        return Key.Parse(reader.GetString()!);
    }

    public override void Write(Utf8JsonWriter writer, Key value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.ToString());
}
