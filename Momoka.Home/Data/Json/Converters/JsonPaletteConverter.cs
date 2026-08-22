using System.Text.Json;
using System.Text.Json.Serialization;
using Momoka.Home.Levels.Entities;
using Momoka.Home.Levels.Layouts;
namespace Momoka.Home.Data.Json.Converters;

/// <summary>
/// Serializes a <see cref="Palette{T}"/> as a JSON array of its values,
/// skipping the reserved empty slot at index 0. Entity palettes write entity
/// <see cref="Entity.Id"/> (Guid) references, keeping payloads independent of
/// the entity list's order — the same convention as the chunk codec. Reading
/// is not supported here (Entity references need the entity table); restore
/// via <see cref="Palette{T}.FromValues"/> at the storage layer.
/// </summary>
public sealed class JsonPaletteConverterFactory : JsonConverterFactory
{
    public override bool CanConvert(Type typeToConvert) =>
        typeToConvert.IsGenericType && typeToConvert.GetGenericTypeDefinition() == typeof(Palette<>);

    public override JsonConverter? CreateConverter(Type typeToConvert, JsonSerializerOptions options)
    {
        var valueType = typeToConvert.GetGenericArguments()[0];
        return (JsonConverter)Activator.CreateInstance(typeof(JsonPaletteConverter<>).MakeGenericType(valueType))!;
    }
}

public sealed class JsonPaletteConverter<T> : JsonConverter<Palette<T>> where T : notnull
{
    public override Palette<T>? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        throw new NotSupportedException("Palette restore needs the entity table — use Palette<T>.FromValues at the storage layer.");

    public override void Write(Utf8JsonWriter writer, Palette<T> value, JsonSerializerOptions options)
    {
        writer.WriteStartArray();
        for (var i = 1; i < value.Size; i++)
        {
            var v = value.ValueFor(i);
            if (v is Entity e)
                writer.WriteStringValue(e.Id);
            else
                JsonSerializer.Serialize(writer, v, options);
        }
        writer.WriteEndArray();
    }
}
