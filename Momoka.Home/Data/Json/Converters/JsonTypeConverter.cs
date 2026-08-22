using Momoka.Home.Data.Json;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
namespace Momoka.Home.Data.Json.Converters;

/// <summary>
/// Shared engine for registry-driven polymorphic converters: writes a
/// <c>{ "kind": …, "data": { …成员… } }</c> envelope and delegates the members to
/// stock Json.NET (<c>data</c> uses a converter-less internal serializer with
/// snake_case + the grid converter, so member serialization honors property-level
/// [JsonConverter] and the registered converters). Reading resolves the concrete
/// type via <see cref="JsonTypeNameRegistry"/> and materializes <c>data</c> with
/// stock Json.NET — no manual member iteration.
/// </summary>
public abstract class JsonTypeConverter<TBase> : JsonConverter where TBase : class
{
    private readonly JsonSerializer _plain;

    protected JsonTypeConverter()
    {
        _plain = JsonSerializer.Create(new JsonSerializerSettings
        {
            ContractResolver = new DefaultContractResolver { NamingStrategy = new SnakeCaseNamingStrategy() },
            Converters = { new JsonGridLayoutConverter() }
        });
    }

    /// <summary>JSON discriminator key ("kind" for geometry/components, "type" for properties).</summary>
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
        if (reader.TokenType == JsonToken.Null)
            return null; // 可空引用（如隐藏实体的 Volume = null）
        var obj = JObject.Load(reader);
        return ReadLoadedObject(obj, objectType);
    }

    /// <summary>从已载入的 JSON 对象物化目标实例（判别 → data 委托 stock Json.NET → 装载钩子）。</summary>
    protected virtual object? ReadLoadedObject(JObject obj, Type declaredType)
    {
        var kind = obj[Discriminator]?.Value<string>() ?? "";
        var targetType = ResolveTargetType(kind, declaredType);
        if (obj["data"] is not JToken data)
            throw new JsonSerializationException($"{declaredType.Name} '{kind}' is missing the 'data' member.");

        object? value;
        try
        {
            value = data.ToObject(targetType, _plain);
        }
        catch (JsonSerializationException ex) when (ex.InnerException is ArgumentException)
        {
            // A value setter's validation error surfaces as-is, not wrapped.
            throw ex.InnerException;
        }
        OnLoaded(value!);
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
        writer.WritePropertyName("data");
        _plain.Serialize(writer, value, value.GetType());
        writer.WriteEndObject();
    }
}
