using Momoka.Home.Entities;
using Momoka.Home.Layouts;
using Momoka.Home.Primitives;
using Momoka.Home.Data.Json;
using Momoka.Home.Data.Json.Converters;
using Newtonsoft.Json;
namespace Momoka.Home.Components;

/// <summary>
/// Capability component: one placement surface (<see cref="GridLayout{T}"/>) an
/// entity provides — a floor slab's top face, a shelf board, a stair tread…
/// Attach multiple instances for objects with several surfaces (bookshelves,
/// stairs). Config-driven.
/// </summary>
[JsonTypeName("placement_layout")]
public class PlacementLayoutSource : Component
{
    /// <summary>放置表面。语义上携带本组件即必提供表面，故恒非空
    /// （JSON 缺省 / 旧数据时取默认空网格，不产生 null）。</summary>
    public GridLayout<bool> Layout { get; set; } = new(Int2.Zero);

    /// <summary>放置在本表面上的物件（表面宿主登记，由 <c>UnitLayout.Add</c>
    /// 登记 / <c>UnitLayout.Remove</c> 反登记；级联回落与"被依赖"检查依赖此表）。
    /// 运行时登记态——暂不序列化（存档加载后依赖关系由管线后置重建，待实现）。</summary>
    [JsonIgnore]
    public List<Entity> Items { get; } = new();
}
