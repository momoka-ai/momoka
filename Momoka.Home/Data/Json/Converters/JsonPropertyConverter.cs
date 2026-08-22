using Momoka.Home.Levels.Entities;
using Momoka.Home.Data.Json;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Momoka.Home.Levels.Entities.Properties;
namespace Momoka.Home.Data.Json.Converters;

/// <summary>
/// Serializes a <see cref="Property"/> via <see cref="JsonTypeConverter{T}"/>: the
/// "type" discriminator is declared by <see cref="JsonTypeNameAttribute"/> and
/// resolved through <see cref="JsonTypeNameRegistry"/>; members live in the
/// "data" envelope, bound by stock Json.NET — no per-kind logic.
/// </summary>
/// <remarks>
/// <see cref="EnumProperty{T}"/> 是唯一泛型属性子类——注册表只收封闭类型，无法从
/// "enum" 名直接物化。此处自描述化：写时附 <c>value_type</c>（枚举 CLR 类型全名），
/// 读时用它闭合泛型再按值构造（默认值 = 序列化值，保留 UnsetValue 语义）。
/// </remarks>
public class JsonPropertyConverter : JsonTypeConverter<Property>
{
    protected override string Discriminator => "type";

    private static bool IsEnumProperty(Type type) =>
        type.IsGenericType && type.GetGenericTypeDefinition() == typeof(EnumProperty<>);

    protected override string NameOf(Type type) =>
        IsEnumProperty(type) ? "enum" : base.NameOf(type);

    public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer)
    {
        if (value is null)
        {
            writer.WriteNull();
            return;
        }
        if (IsEnumProperty(value.GetType()))
        {
            var property = (Property)value;
            writer.WriteStartObject();
            writer.WritePropertyName("type");
            writer.WriteValue("enum");
            writer.WritePropertyName("data");
            writer.WriteStartObject();
            writer.WritePropertyName("value_type");
            writer.WriteValue(property.ValueType.AssemblyQualifiedName);
            writer.WritePropertyName("key");
            writer.WriteValue(property.Name);
            if (property.BoxedValue is { } boxed)
            {
                writer.WritePropertyName("value");
                serializer.Serialize(writer, boxed);
            }
            writer.WriteEndObject();
            writer.WriteEndObject();
            return;
        }
        base.WriteJson(writer, value, serializer);
    }

    public override object? ReadJson(JsonReader reader, Type objectType, object? existingValue, JsonSerializer serializer)
    {
        var obj = JObject.Load(reader);
        var data = obj["data"] as JObject;
        if (obj["type"]?.Value<string>() == "enum")
        {
            var valueTypeName = data?["value_type"]?.Value<string>()
                ?? throw new JsonSerializationException("Enum property missing 'value_type'.");
            var enumType = Type.GetType(valueTypeName)
                ?? throw new JsonSerializationException($"Unknown enum type '{valueTypeName}'.");
            var targetType = typeof(EnumProperty<>).MakeGenericType(enumType);
            var ctor = targetType.GetConstructor(new[] { typeof(string), enumType, typeof(string) })
                ?? throw new JsonSerializationException($"No enum property constructor for '{valueTypeName}'.");

            object? defaultValue = Activator.CreateInstance(enumType);
            if (data?["value"] is JToken valueToken && valueToken.Type != JTokenType.Null)
                defaultValue = valueToken.ToObject(enumType, serializer);

            return (Property)ctor.Invoke(new[] { data?["key"]?.Value<string>() ?? "", defaultValue, "" })!;
        }
        return ReadLoadedObject(obj, objectType);
    }
}
