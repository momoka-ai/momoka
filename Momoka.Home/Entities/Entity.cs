using Momoka.Home;
using Momoka.Home.Components;
using Momoka.Home.Geometry;
using Momoka.Home.Primitives;
using Momoka.Home.Properties;
using Newtonsoft.Json;
namespace Momoka.Home.Entities;

/// <summary>
/// A 3D spatial entity — the single entity type of the flattened model.
/// Carries behavior components (<see cref="Components"/>) and per-instance
/// properties (<see cref="Properties"/>; config-driven — entities get cloned
/// properties from their template at materialization), a position
/// (<see cref="Pos"/>) in its parent 3D space and a body geometry
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

    /// <summary>Position of the entity in its parent 3D space (absolute cm; <see cref="Position.Scale"/> = 1).</summary>
    [JsonProperty("position")]
    public Position Pos { get; set; }

    /// <summary>Body geometry: which voxel cells this entity occupies (null for pure markers).</summary>
    [JsonProperty("volume")]
    public Volume Volume { get; set; } = null!;

    /// <summary>物件的接触面：自己哪个面贴合放置表面（法向须与宿主表面法向相反）。
    /// 必须轴对齐（体素物件 6 向，见 <see cref="Direction.IsAxisAligned"/>）；
    /// 缺省 Down（底面接触，普通物件）。</summary>
    [JsonProperty("contact_face")]
    public Direction ContactFace { get; set; } = Direction.Down;

    [JsonProperty("components")]
    public List<Component> Components { get; } = new();

    [JsonProperty("properties")]
    public List<Property> Properties { get; set; } = new();

    public event EventHandler<PropertyValueChangedEventArgs>? PropertyValueChanged;

    public void NotifyPropertyChanged(Property property, object? newValue) =>
        PropertyValueChanged?.Invoke(this, new PropertyValueChangedEventArgs(property, newValue));
}
