using Momoka.Home;
using Momoka.Home.Entities;
using Momoka.Home.Geometry;
using Momoka.Home.Primitives;
using Newtonsoft.Json;
namespace Momoka.Home.Entities;

/// <summary>
/// A 3D spatial entity — the single entity type of the flattened model.
/// Carries behavior components (<see cref="Components"/>) and per-instance
/// properties (<see cref="Properties"/>; config-driven — entities get cloned
/// properties from their template at materialization), a position
/// (<see cref="Transform"/>) in its parent 3D space and a body geometry
/// (<see cref="Volume"/>). Directly JSON-serializable — see
/// <see cref="Momoka.Home.Settings.JsonSerialization"/> — with each field
/// bound by <c>JsonProperty</c>. Component and property operations are
/// provided once, as extension methods on <see cref="IComponentSource"/> and
/// <see cref="IPropertySource"/>.
/// </summary>
public class Entity : IComponentSource, IPropertySource
{
    [JsonProperty("id")]
    public Guid Id { get; init; } = Guid.NewGuid();

    [JsonProperty("key")]
    public Key Key { get; init; }

    /// <summary>姿态：位置（世界 cm）+ 旋转（<see cref="Rotation"/>，三轴欧拉 YXZ）。
    /// 放置后由 <c>UnitLayout.Add</c> 写入；缺省 Identity（原点、零旋转）。</summary>
    [JsonProperty("transform")]
    public Transform Transform { get; set; } = Transform.Identity;

    /// <summary>Body geometry: which voxel cells this entity occupies (null for pure markers).</summary>
    [JsonProperty("volume")]
    public Volume Volume { get; set; } = null!;

    [JsonProperty("components")]
    public List<Component> Components { get; } = new();

    [JsonProperty("properties")]
    public List<Property> Properties { get; set; } = new();

    public event EventHandler<PropertyValueChangedEventArgs>? PropertyValueChanged;

    public void NotifyPropertyChanged(Property property, object? newValue) =>
        PropertyValueChanged?.Invoke(this, new PropertyValueChangedEventArgs(property, newValue));
}
