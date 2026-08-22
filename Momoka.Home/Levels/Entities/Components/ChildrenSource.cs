using System.Text.Json.Serialization;
using Momoka.Home.Data.Json;
using Momoka.Home.Data.Json.Converters;
using Momoka.Home.Levels.Entities;
namespace Momoka.Home.Levels.Entities.Components;

/// <summary>
/// 容器组件：持有子实体（直接引用）。墙系统等"同类物体的超集"用本组件挂载成员；
/// <see cref="PlacementLayoutSource"/> 继承本组件（表面物件也是一种子实体）。
/// 级联删除 / 随移 / 装载重链统一走 <see cref="Children"/>。
/// </summary>
[JsonTypeName("children")]
public class ChildrenSource : Component
{
    /// <summary>子实体（内存真相，直接引用；级联 / 随移 / 反向索引走本表）。
    /// 序列化由 <see cref="JsonEntityIdListConverter"/> 处理——只写 Id 不内嵌实体载荷；
    /// 读回为 id-stub，装载时由 <c>LevelLayout.RestorePlacementFromGrid</c> 按 Id 重链。</summary>
    [JsonConverter(typeof(JsonEntityIdListConverter))]
    public List<Entity> Children { get; set; } = new();
}
