using Momoka.Home.States;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
namespace Momoka.Home.Serialization;

/// <summary>
/// Serializes a <see cref="Property"/> to/from its declarative JSON form: the
/// "type" discriminator (declared via <see cref="JsonTypeNameAttribute"/>) plus
/// key and typed value. Members bind directly with snake_case naming — no
/// per-kind logic. The typed <see cref="Property{T}.Value"/> converts "value"
/// straight to the concrete CLR type (never a JToken).
/// </summary>
public class JsonPropertyConverter : JsonConverter
{
    private static readonly JsonSerializer Serializer = JsonSerializer.Create(new JsonSerializerSettings
    {
        ContractResolver = new DefaultContractResolver { NamingStrategy = new SnakeCaseNamingStrategy() },
        Converters = { new JsonPropertyConverter() }
    });

    public override bool CanConvert(Type objectType) =>
        typeof(Property).IsAssignableFrom(objectType);

    public override object? ReadJson(JsonReader reader, Type objectType, object? existingValue, JsonSerializer serializer)
    {
        var obj = JObject.Load(reader);
        var kind = obj["type"]?.Value<string>() ?? "";
        var targetType = JsonTypeNameRegistry.TypeOf<Property>(kind);

        var value = CreateInstance(targetType);
        if (Serializer.ContractResolver.ResolveContract(targetType) is JsonObjectContract contract)
        {
            foreach (var property in contract.Properties)
            {
                if (!property.Writable || property.Ignored)
                    continue;
                if (obj[property.PropertyName!] is not JToken token)
                    continue;
                try
                {
                    property.ValueProvider!.SetValue(value, token.ToObject(property.PropertyType, Serializer));
                }
                catch (JsonSerializationException ex) when (ex.InnerException is ArgumentException)
                {
                    // A value setter's validation error (e.g. a literal outside its
                    // closed set) must surface as-is, not wrapped.
                    throw ex.InnerException;
                }
            }
        }
        ((Property)value).OnConfigLoaded();
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
        writer.WritePropertyName("type");
        writer.WriteValue(JsonTypeNameRegistry.NameOf<Property>(value.GetType()));

        if (Serializer.ContractResolver.ResolveContract(value.GetType()) is JsonObjectContract contract)
        {
            foreach (var property in contract.Properties)
            {
                if (!property.Readable || property.Ignored)
                    continue;
                if (property.ShouldSerialize is not null && !property.ShouldSerialize(value))
                    continue;
                var propertyValue = property.ValueProvider!.GetValue(value);
                if (propertyValue is null && property.NullValueHandling == NullValueHandling.Ignore)
                    continue;
                writer.WritePropertyName(property.PropertyName!);
                Serializer.Serialize(writer, propertyValue, property.PropertyType);
            }
        }

        writer.WriteEndObject();
    }

    /// <summary>
    /// Builds the target instance via its parameterless ctor (falling back to the
    /// longest ctor) — members are then populated by the loop above.
    /// </summary>
    private static object CreateInstance(Type type)
    {
        var ctor = type.GetConstructor(Type.EmptyTypes)
            ?? type.GetConstructors().OrderByDescending(c => c.GetParameters().Length).First();
        return ctor.Invoke(null)!;
    }
}
