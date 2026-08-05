using Momoka.Home.States;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
namespace Momoka.Home.Serialization;

/// <summary>
/// Serializes a <see cref="Property"/> to/from its declarative JSON form: the
/// "type" discriminator (declared via <see cref="JsonTypeNameAttribute"/>) plus
/// key, optional initial value and optional closed value set ("literals" — a
/// <see cref="StringProperty"/> with a values list). Replaces the old
/// PropertyDto + PropertyFactory.
/// </summary>
public class JsonPropertyConverter : JsonConverter<Property>
{
    public override Property? ReadJson(JsonReader reader, Type objectType, Property? existingValue, bool hasExistingValue, JsonSerializer serializer)
    {
        var obj = JObject.Load(reader);
        var key = obj["key"]?.Value<string>() ?? throw new InvalidDataException("Property is missing 'key'.");
        var type = obj["type"]?.Value<string>() ?? "";
        var value = obj["value"];

        if (type == "literals")
        {
            return new StringProperty(key)
            {
                ValidValues = obj["values"]?.ToObject<List<string>>(),
                Value = value?.Value<string>()
            };
        }

        var property = CreateProperty(type, key);
        if (value is not null)
            property.Value = value.ToObject(property.PropertyType);
        return property;
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

    /// <summary>
    /// Instantiates the property type registered under <paramref name="type"/>,
    /// passing the key to its leading name parameter (optional trailing params
    /// fall back to their defaults).
    /// </summary>
    private static Property CreateProperty(string type, string key)
    {
        if (!JsonTypeNameRegistry.TryGetType<Property>(type, out var propertyType))
            throw new NotSupportedException($"Unknown property type '{type}'.");

        var ctor = propertyType.GetConstructors()
            .OrderByDescending(c => c.GetParameters().Length)
            .First();
        var parameters = ctor.GetParameters();
        var args = new object?[parameters.Length];
        args[0] = key;
        for (var i = 1; i < parameters.Length; i++)
            args[i] = parameters[i].HasDefaultValue ? parameters[i].DefaultValue : null;
        return (Property)ctor.Invoke(args)!;
    }

    private static string TypeName(Property property) =>
        property is StringProperty { ValidValues: not null }
            ? "literals"
            : JsonTypeNameRegistry.NameOf<Property>(property.GetType());
}
