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
    /// <summary>子实体（内存真相，直接引用；级联 / 随移 / 反向索引走本表）。</summary>
    [JsonIgnore]
    public List<Entity> Children { get; } = new();

    /// <summary>
    /// 持久化成员 Id——**派生自 <see cref="Children"/>（单一真相源）**：序列化只写 Id，
    /// 不内嵌实体载荷（避免 <c>Entity → Components → Children → Entity</c> 循环）。
    /// 反序列化时 setter 以 Id 物化临时占位实体（stub），装载时由
    /// <c>LevelLayout.RestorePlacementFromGrid</c> 按 Id 重链为注册表真实实体。
    /// </summary>
    [JsonProperty("children")]
    public List<Guid> ChildrenIds
    {
        get => Children.Select(c => c.Id).ToList();
        set
        {
            Children.Clear();
            if (value is null)
                return;
            foreach (var id in value)
                Children.Add(new Entity { Id = id }); // 反序列化 id-stub——装载时重链
        }
    }
}
