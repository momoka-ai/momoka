using System.Text.Json;
using System.Text.Json.Serialization;
using Momoka.Home.Levels.Layouts;
using Momoka.Home.Primitives;
namespace Momoka.Home.Data.Json.Converters;

/// <summary>
/// Serializes a <see cref="GridLayout{T}"/> of bools: size, unit length and
/// the cell array (row-major over XZ). Attached via [JsonConverter] on
/// <see cref="Momoka.Home.Levels.Entities.Components.PlacementLayoutSource.Layout"/> so nested component
/// serialization resolves it. 姿态（Transform）随组件成员序列化，不在此。
/// </summary>
public class JsonGridLayoutConverter : JsonConverter<GridLayout<bool>>
{
    public override GridLayout<bool>? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
            return null;

        using var doc = JsonDocument.ParseValue(ref reader);
        var root = doc.RootElement;
        var sizeToken = root.GetProperty("size");
        var size = new Int2(sizeToken[0].GetInt32(), sizeToken[1].GetInt32());
        var unitLength = root.TryGetProperty("unit_length", out var unit) ? unit.GetSingle() : 10f;
        var cells = root.GetProperty("cells");

        var grid = new GridLayout<bool>(size) { UnitLength = unitLength };
        var i = 0;
        for (var z = 0; z < size.Z; z++)
            for (var x = 0; x < size.X; x++)
                grid[new Int2(x, z)] = cells[i++].GetBoolean();
        return grid;
    }

    public override void Write(Utf8JsonWriter writer, GridLayout<bool> value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WritePropertyName("size");
        writer.WriteStartArray();
        writer.WriteNumberValue(value.Size.X);
        writer.WriteNumberValue(value.Size.Z);
        writer.WriteEndArray();
        writer.WriteNumber("unit_length", value.UnitLength);
        writer.WritePropertyName("cells");
        writer.WriteStartArray();
        for (var z = 0; z < value.Size.Z; z++)
            for (var x = 0; x < value.Size.X; x++)
                writer.WriteBooleanValue(value[new Int2(x, z)]);
        writer.WriteEndArray();
        writer.WriteEndObject();
    }
}
