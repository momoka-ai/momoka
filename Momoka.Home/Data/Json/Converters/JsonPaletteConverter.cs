using Momoka.Home.Levels.Entities;
using Momoka.Home.Levels.Layouts;
using Newtonsoft.Json;
namespace Momoka.Home.Data.Json.Converters;

/// <summary>
/// Serializes a <see cref="Palette{T}"/> as a JSON array of its values,
/// skipping the reserved empty slot at index 0. Entity palettes write entity
/// <see cref="Entity.Id"/> (Guid) references, keeping payloads independent of
/// the entity list's order — the same convention as the chunk codec. Reading
/// is not supported here (Entity references need the entity table); restore
/// via <see cref="Palette{T}.FromValues"/> at the storage layer.
/// </summary>
public class JsonPaletteConverter : JsonConverter
{
    public override bool CanConvert(Type objectType) =>
        objectType.IsGenericType && objectType.GetGenericTypeDefinition() == typeof(Palette<>);

    public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer)
    {
        if (value is null)
        {
            writer.WriteNull();
            return;
        }

        var type = value.GetType();
        var size = (int)type.GetProperty(nameof(Palette<int>.Size))!.GetValue(value)!;
        var valueFor = type.GetMethod(nameof(Palette<int>.ValueFor))!;

        writer.WriteStartArray();
        for (var i = 1; i < size; i++)
        {
            var v = valueFor.Invoke(value, new object[] { i });
            if (v is Entity e)
                writer.WriteValue(e.Id);
            else
                serializer.Serialize(writer, v);
        }
        writer.WriteEndArray();
    }

    public override object? ReadJson(JsonReader reader, Type objectType, object? existingValue, JsonSerializer serializer) =>
        throw new NotSupportedException("Palette restore needs the entity table — use Palette<T>.FromValues at the storage layer.");
}
