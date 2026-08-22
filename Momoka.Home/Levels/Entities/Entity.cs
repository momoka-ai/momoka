using Momoka.Home;
using Momoka.Home.Levels.Entities;
using Momoka.Home.Levels.Volumes;
using Momoka.Home.Primitives;
using System.Text.Json.Serialization;
using Momoka.Home.Levels.Entities.Components;
using Momoka.Home.Levels.Entities.Properties;
namespace Momoka.Home.Levels.Entities;

/// <summary>
/// A 3D spatial entity — the single entity type of the flattened model.
/// Carries behavior components (<see cref="Components"/>) and per-instance
/// properties (<see cref="Properties"/>; config-driven — entities get cloned
/// properties from their template at materialization), a position
/// (<see cref="Transform"/>) in its parent 3D space and a body geometry
/// (<see cref="Volume"/>). Directly JSON-serializable — see
/// <see cref="Momoka.Home.Data.Settings.JsonSerialization"/> — with snake_case
/// naming and registry-driven polymorphism. Component and property operations
/// are provided once, as extension methods on <see cref="IComponentSource"/> and
/// <see cref="IPropertySource"/>.
/// </summary>
public class Entity : IComponentSource, IPropertySource
{
    public Guid Id { get; init; } = Guid.NewGuid();

    public Key Key { get; init; }

    /// <summary>姿态：位置（世界 cm）+ 旋转（<see cref="Rotation"/>，三轴欧拉 YXZ）。
    /// 放置后由 <c>LevelLayout.Add</c> 写入；缺省 Identity（原点、零旋转）。</summary>
    public Transform Transform { get; set; } = Transform.Identity;

    /// <summary>Body geometry: which voxel cells this entity occupies (null for pure markers).</summary>
    public Volume Volume { get; set; } = null!;

    public List<Component> Components { get; } = new();

    public List<Property> Properties { get; set; } = new();

    public event EventHandler<PropertyValueChangedEventArgs>? PropertyValueChanged;

    public void NotifyPropertyChanged(Property property, object? newValue) =>
        PropertyValueChanged?.Invoke(this, new PropertyValueChangedEventArgs(property, newValue));
}
