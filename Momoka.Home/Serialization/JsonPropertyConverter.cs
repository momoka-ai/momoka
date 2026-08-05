using Momoka.Home.States;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
namespace Momoka.Home.Serialization;

/// <summary>
/// Serializes a <see cref="Property"/> to/from its declarative JSON form: the
/// "type" discriminator plus key, optional initial value and optional closed
/// value set. Replaces the old PropertyDto + PropertyFactory.
/// </summary>
public class JsonPropertyConverter : JsonConverter<Property>
{
    public override Property? ReadJson(JsonReader reader, Type objectType, Property? existingValue, bool hasExistingValue, JsonSerializer serializer)
    {
        var obj = JObject.Load(reader);
        var key = obj["key"]?.Value<string>() ?? throw new InvalidDataException("Property is missing 'key'.");
        var type = obj["type"]?.Value<string>() ?? "";
        var value = obj["value"];

        return type switch
        {
            "boolean" => new BooleanProperty(key) { Value = value?.Value<bool>() },
            "int" => new IntProperty(key) { Value = value?.Value<int>() },
            "float" => new FloatProperty(key) { Value = value?.Value<float>() },
            "string" => new StringProperty(key) { Value = value?.Value<string>() },
            "texture" => new TextureProperty(key) { Value = value?.Value<string>() },
            "literals" => new StringProperty(key)
            {
                ValidValues = obj["values"]?.ToObject<List<string>>(),
                Value = value?.Value<string>()
            },
            _ => throw new NotSupportedException($"Unknown property type '{type}'.")
        };
    }

    public override void WriteJson(JsonWriter writer, Property? value, JsonSerializer serializer)
    {
        if (value is null)
        {
            writer.WriteNull();
            return;
        }

        writer.WriteStartObject();
        writer.WritePropertyName("key");
        writer.WriteValue(value.Name);
        writer.WritePropertyName("type");
        writer.WriteValue(TypeName(value));

        if (value.Value is not null)
        {
            writer.WritePropertyName("value");
            writer.WriteValue(value.Value);
        }

        if (value.ValidValues is { } validValues)
        {
            writer.WritePropertyName("values");
            serializer.Serialize(writer, validValues);
        }

        writer.WriteEndObject();
    }

    private static string TypeName(Property property) => property switch
    {
        BooleanProperty => "boolean",
        IntProperty => "int",
        FloatProperty => "float",
        TextureProperty => "texture",
        StringProperty => property.ValidValues is null ? "string" : "literals",
        _ => "string"
    };
}
