using Momoka.Home.Levels.Entities;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
namespace Momoka.Home.Data.Json.Converters;

/// <summary>
/// 实体引用列表 ↔ Id 数组（局部转换器，属性级 <c>[JsonConverter]</c> 标注使用）：
/// 只序列化 Id，不内嵌实体载荷（避免 <c>Entity → Components → Children → Entity</c>
/// 循环）。读回时以 Id 物化临时占位实体（stub）——若目标列表已存在（只读属性，
/// 转换器原地改写 existingValue）则清空填充；装载时由
/// <c>LevelLayout.RestorePlacementFromGrid</c> 按 Id 重链为注册表真实实体。
/// </summary>
public class JsonEntityIdListConverter : JsonConverter<List<Entity>>
{
    public override void WriteJson(JsonWriter writer, List<Entity>? value, JsonSerializer serializer)
    {
        writer.WriteStartArray();
        if (value is not null)
            foreach (var entity in value)
                writer.WriteValue(entity.Id);
        writer.WriteEndArray();
    }

    public override List<Entity> ReadJson(JsonReader reader, Type objectType, List<Entity>? existingValue, bool hasExistingValue, JsonSerializer serializer)
    {
        var result = existingValue ?? new List<Entity>();
        result.Clear();
        if (reader.TokenType == JsonToken.Null)
            return result;

        var array = JArray.Load(reader);
        foreach (var token in array)
            result.Add(new Entity { Id = token.ToObject<Guid>() }); // 反序列化 id-stub——装载时重链
        return result;
    }
}
