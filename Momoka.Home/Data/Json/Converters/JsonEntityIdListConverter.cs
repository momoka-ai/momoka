using System.Text.Json;
using System.Text.Json.Serialization;
using Momoka.Home.Levels.Entities;
namespace Momoka.Home.Data.Json.Converters;

/// <summary>
/// 实体引用列表 ↔ Id 数组（局部转换器，属性级 <c>[JsonConverter]</c> 标注使用）：
/// 只序列化 Id，不内嵌实体载荷（避免 <c>Entity → Components → Children → Entity</c>
/// 循环）。读回时以 Id 物化临时占位实体（stub）；装载时由
/// <c>LevelLayout.RestorePlacementFromGrid</c> 按 Id 重链为注册表真实实体。
/// </summary>
public class JsonEntityIdListConverter : JsonConverter<List<Entity>>
{
    public override List<Entity> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var result = new List<Entity>();
        if (reader.TokenType == JsonTokenType.Null)
            return result;
        if (reader.TokenType != JsonTokenType.StartArray)
            throw new JsonException("Entity id list must be an array.");

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndArray)
                break;
            result.Add(new Entity { Id = reader.GetGuid() }); // 反序列化 id-stub——装载时重链
        }
        return result;
    }

    public override void Write(Utf8JsonWriter writer, List<Entity>? value, JsonSerializerOptions options)
    {
        if (value is null)
        {
            writer.WriteNullValue();
            return;
        }

        writer.WriteStartArray();
        foreach (var entity in value)
            writer.WriteStringValue(entity.Id);
        writer.WriteEndArray();
    }
}
