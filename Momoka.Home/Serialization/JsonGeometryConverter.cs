using Momoka.Home.Geometry;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
namespace Momoka.Home.Serialization;

/// <summary>
/// Serializes <see cref="Volume"/> (and the 2D <see cref="Shape"/> footprints they
/// embed) to/from the declarative JSON form. The "kind" discriminator is declared
/// by <see cref="JsonTypeNameAttribute"/> and resolved through
/// <see cref="JsonTypeNameRegistry"/>; all params bind directly to the type's
/// properties with snake_case naming — no per-kind codecs or manual writers.
/// Adding a shape = one [JsonTypeName] attribute on the class, nothing else.
/// </summary>
public class JsonGeometryConverter : JsonConverter
{
    private static readonly JsonSerializer Serializer = JsonSerializer.Create(new JsonSerializerSettings
    {
        ContractResolver = new DefaultContractResolver { NamingStrategy = new SnakeCaseNamingStrategy() },
        Converters = { new JsonGeometryConverter() }
    });

    public override bool CanConvert(Type objectType) =>
        typeof(Volume).IsAssignableFrom(objectType) || typeof(Shape).IsAssignableFrom(objectType);

    public override object? ReadJson(JsonReader reader, Type objectType, object? existingValue, JsonSerializer serializer)
    {
        var obj = JObject.Load(reader);
        var kind = obj["kind"]?.Value<string>() ?? "";
        var targetType = typeof(Volume).IsAssignableFrom(objectType)
            ? JsonTypeNameRegistry.TypeOf<Volume>(kind)
            : JsonTypeNameRegistry.TypeOf<Shape>(kind);

        var value = CreateInstance(targetType);
        if (Serializer.ContractResolver.ResolveContract(targetType) is JsonObjectContract contract)
        {
            foreach (var property in contract.Properties)
            {
                if (!property.Writable || property.Ignored)
                    continue;
                if (obj[property.PropertyName!] is not JToken token)
                    continue;
                property.ValueProvider!.SetValue(value, token.ToObject(property.PropertyType, Serializer));
            }
        }
        return value;
    }

    public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer)
    {
        if (value is null)
        {
            writer.WriteNull();
            return;
        }

        writer.WriteStartObject();
        writer.WritePropertyName("kind");
        writer.WriteValue(value is Volume
            ? JsonTypeNameRegistry.NameOf<Volume>(value.GetType())
            : JsonTypeNameRegistry.NameOf<Shape>(value.GetType()));

        if (Serializer.ContractResolver.ResolveContract(value.GetType()) is JsonObjectContract contract)
        {
            foreach (var property in contract.Properties)
            {
                if (!property.Readable || property.Ignored)
                    continue;
                if (property.ShouldSerialize is not null && !property.ShouldSerialize(value))
                    continue;
                writer.WritePropertyName(property.PropertyName!);
                Serializer.Serialize(writer, property.ValueProvider!.GetValue(value), property.PropertyType);
            }
        }

        writer.WriteEndObject();
    }

    /// <summary>
    /// Builds the target instance via its parameterless ctor (falling back to the
    /// longest ctor) — fields are then populated by the member loop above.
    /// </summary>
    private static object CreateInstance(Type type)
    {
        var ctor = type.GetConstructor(Type.EmptyTypes)
            ?? type.GetConstructors().OrderByDescending(c => c.GetParameters().Length).First();
        return ctor.Invoke(null)!;
    }
}
