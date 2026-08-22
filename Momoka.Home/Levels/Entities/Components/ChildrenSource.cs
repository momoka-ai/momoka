using Momoka.Home.Data.Json;
using Momoka.Home.Levels.Entities;
using Newtonsoft.Json;
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
    /// 序列化走 <see cref="ChildrenIds"/>——不内嵌实体载荷，避免
    /// <c>Entity → Components → Children → Entity</c> 循环。</summary>
    [JsonIgnore]
    public List<Entity> Children { get; } = new();

    /// <summary>持久化：成员 Id 列表。装载时由 <c>LevelLayout.RestorePlacementFromGrid</c>
    /// 按 Id 重链进 <see cref="Children"/>。</summary>
    [JsonProperty("children")]
    public List<Guid> ChildrenIds { get; set; } = new();

    /// <summary>登记子实体（同步持久化 Id 表）。</summary>
    public void AddChild(Entity entity)
    {
        Children.Add(entity);
        ChildrenIds.Add(entity.Id);
    }

    /// <summary>移除子实体（同步持久化 Id 表）。</summary>
    public bool RemoveChild(Entity entity)
    {
        Children.Remove(entity);
        return ChildrenIds.Remove(entity.Id);
    }

    /// <summary>清空子实体与持久化 Id 表。</summary>
    public void ClearChildren()
    {
        Children.Clear();
        ChildrenIds.Clear();
    }
}
