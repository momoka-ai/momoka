using Momoka.Home;
using Momoka.Home.Components;
using Momoka.Home.Geometry;
using Momoka.Home.Primitives;
using Momoka.Home.Properties;
namespace Momoka.Home.Entities;

/// <summary>
/// A 3D spatial entity — the single entity type of the flattened model.
/// Carries behavior components (<see cref="Components"/>) and per-instance
/// properties (<see cref="Properties"/>; config-driven — entities get cloned
/// properties from their template at materialization), a position
/// (<see cref="Coords"/>) in its parent 3D space and a body geometry
/// (<see cref="Volume"/>). Component and property operations are provided once,
/// as extension methods on <see cref="IComponentSource"/> and
/// <see cref="IPropertySource"/>.
/// </summary>
public class Entity : IComponentSource, IPropertySource
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Key Key { get; init; }

    /// <summary>Position of the entity in its parent 3D space.</summary>
    public Int3 Coords { get; set; }

    /// <summary>Body geometry: which voxel cells this entity occupies (null for pure markers).</summary>
    public Volume Volume { get; set; } = null!;

    // ── Behavior components ──────────────────────────────

    public IList<Component> Components => _components;
    private readonly List<Component> _components = new();

    // ── Properties (per-instance, config-driven) ─────────

    public IList<Property> Properties => _properties;
    private readonly List<Property> _properties = new();

    public event EventHandler<PropertyValueChangedEventArgs>? PropertyValueChanged;

    public void NotifyPropertyChanged(Property property, object? newValue) =>
        PropertyValueChanged?.Invoke(this, new PropertyValueChangedEventArgs(property, newValue));
}
