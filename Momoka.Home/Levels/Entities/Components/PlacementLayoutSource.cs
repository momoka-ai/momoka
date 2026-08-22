using System.Text.Json.Serialization;
using Momoka.Home.Levels.Entities;
using Momoka.Home.Levels.Layouts;
using Momoka.Home.Primitives;
using Momoka.Home.Data.Json;
using Momoka.Home.Data.Json.Converters;
namespace Momoka.Home.Levels.Entities.Components;

/// <summary>
/// Capability component: one placement surface an entity provides — a floor
/// slab's top face, a shelf board, a stair tread… Attach multiple instances for
/// objects with several surfaces (bookshelves, stairs). Config-driven.
/// 表面 = 纯局部格网（<see cref="Layout"/>）+ 姿态（<see cref="Transform"/>，
/// 位置 + 朝向）；<see cref="AsAbsolute"/> 把局部格映射到世界格。
/// 继承 <see cref="ChildrenSource"/>——表面上的物件即子实体（<c>Children</c>）。
/// </summary>
[JsonTypeName("placement_layout")]
public class PlacementLayoutSource : ChildrenSource
{
    /// <summary>放置表面格网（局部坐标，纯数据——位置 / 朝向见 <see cref="Transform"/>）。
    /// 语义上携带本组件即必提供表面，故恒非空（JSON 缺省 / 旧数据时取默认空网格）。</summary>
    [JsonConverter(typeof(JsonGridLayoutConverter))]
    public GridLayout<bool> Layout { get; set; } = new(Int2.Zero);

    /// <summary>表面姿态：位置（世界 cm）+ 朝向。缺省 Identity（原点朝上）。</summary>
    public Transform Transform { get; set; } = Transform.Identity;

    /// <summary>把局部格映射到世界格（根绝对）：姿态行轴 / 列轴 × 格长 + 位置，取整到格。
    /// Up 面（Identity）下 rel 映射为 Position/UnitLength + (rel.X, 0, rel.Z)。
    /// 调用约束（调用方须保证，否则映射错位）：
    /// - <see cref="GridLayout{T}.UnitLength"/> 必须等于宿主体素格长
    ///   （<c>LevelLayout.Voxels.Length</c>，默认 10cm）——本方法结果被当宿主体素格
    ///   坐标直接使用（如 <c>Region</c> 的站立格采集），两种格长独立配置会错位；
    /// - <see cref="Transform.Position"/> 须为 UnitLength 整数倍——否则取整漂移。</summary>
    public Int3 AsAbsolute(Int2 rel)
    {
        var u = Transform.Rotation.RowAxis;
        var v = Transform.Rotation.ColumnAxis;
        var w = Transform.Position + u * (rel.X * Layout.UnitLength) + v * (rel.Z * Layout.UnitLength);
        return new Int3(
            (int)Math.Round(w.X / Layout.UnitLength),
            (int)Math.Round(w.Y / Layout.UnitLength),
            (int)Math.Round(w.Z / Layout.UnitLength));
    }
}
