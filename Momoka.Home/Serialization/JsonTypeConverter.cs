using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
namespace Momoka.Home.Serialization;

/// <summary>
/// Shared engine for registry-driven polymorphic converters: reads the JSON
/// discriminator ("kind"/"type"), resolves the concrete type via
/// <see cref="JsonTypeNameRegistry"/>, then binds members directly with snake_case
/// naming — no per-kind codecs or manual writers. The top-level object is never
/// re-entered into the serializer (CreateInstance + member iteration), so nested
/// polymorphic members recurse safely down the tree.
/// </summary>
public abstract class JsonTypeConverter<TBase> : JsonConverter where TBase : class
{
    private readonly JsonSerializer _serializer;

    protected JsonTypeConverter()
    {
        _serializer = JsonSerializer.Create(new JsonSerializerSettings
        {
            ContractResolver = new DefaultContractResolver { NamingStrategy = new SnakeCaseNamingStrategy() },
            Converters = { this }
        });
    }

    /// <summary>JSON discriminator key ("kind" for geometry, "type" for properties).</summary>
    protected abstract string Discriminator { get; }

    /// <summary>Resolves the concrete type for a discriminator value (registry family).</summary>
    protected virtual Type ResolveTargetType(string kind, Type declaredType) =>
        JsonTypeNameRegistry.TypeOf<TBase>(kind);

    /// <summary>The discriminator value for a runtime type (registry family).</summary>
    protected virtual string NameOf(Type type) =>
        JsonTypeNameRegistry.NameOf<TBase>(type);

    /// <summary>Post-load hook (e.g. Property.OnConfigLoaded cross-field validation).</summary>
    protected virtual void OnLoaded(object value) { }

    public override bool CanConvert(Type objectType) =>
        typeof(TBase).IsAssignableFrom(objectType);

    public override object? ReadJson(JsonReader reader, Type objectType, object? existingValue, JsonSerializer serializer)
    {
        var obj = JObject.Load(reader);
        var kind = obj[Discriminator]?.Value<string>() ?? "";
        var targetType = ResolveTargetType(kind, objectType);

        var value = CreateInstance(targetType);
        FillMembers(obj, targetType, value);
        OnLoaded(value);
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
        writer.WritePropertyName(Discriminator);
        writer.WriteValue(NameOf(value.GetType()));

        if (_serializer.ContractResolver.ResolveContract(value.GetType()) is JsonObjectContract contract)
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
                _serializer.Serialize(writer, propertyValue, property.PropertyType);
            }
        }

        writer.WriteEndObject();
    }

    private void FillMembers(JObject obj, Type targetType, object value)
    {
        if (_serializer.ContractResolver.ResolveContract(targetType) is not JsonObjectContract contract)
            return;

        foreach (var property in contract.Properties)
        {
            if (!property.Writable || property.Ignored)
                continue;
            if (obj[property.PropertyName!] is not JToken token)
                continue;
            try
            {
                property.ValueProvider!.SetValue(value, token.ToObject(property.PropertyType, _serializer));
            }
            catch (JsonSerializationException ex) when (ex.InnerException is ArgumentException)
            {
                // A value setter's validation error surfaces as-is, not wrapped.
                throw ex.InnerException;
            }
        }
    }

    /// <summary>
    /// Builds the target instance via its parameterless ctor (falling back to the
    /// longest ctor) — members are then populated by the fill loop.
    /// </summary>
    private object CreateInstance(Type type)
    {
        var ctor = type.GetConstructor(Type.EmptyTypes)
            ?? type.GetConstructors().OrderByDescending(c => c.GetParameters().Length).First();
        return ctor.Invoke(null)!;
    }
}
