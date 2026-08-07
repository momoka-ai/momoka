using Momoka.Home;
using Momoka.Home.Components;
using Momoka.Home.Geometry;
using Momoka.Home.Primitives;
using Momoka.Home.States;
namespace Momoka.Home.Entities;

/// <summary>
/// A 3D spatial entity — the single entity type of the flattened model.
/// Carries behavior components, per-instance properties (config-driven: entities
/// get cloned properties from their template at materialization; each
/// <see cref="Property"/> holds its own <see cref="Property.Value"/>), a
/// position (<see cref="Coords"/>) in its parent 3D space and a body geometry
/// (<see cref="Volume"/>).
/// </summary>
public class Entity : IComponentSource
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Key Key { get; init; }

    /// <summary>Position of the entity in its parent 3D space.</summary>
    public Int3 Coords { get; set; }

    /// <summary>Body geometry: which voxel cells this entity occupies (null for pure markers).</summary>
    public Volume Volume { get; set; } = null!;

    // ── Properties (per-instance, config-driven) ─────────

    private readonly List<Property> _properties = new();

    public event EventHandler<PropertyValueChangedEventArgs>? PropertyValueChanged;

    /// <summary>Declares properties on this instance (definition + default).</summary>
    protected void AddProperty(params Property[] properties)
    {
        _properties.AddRange(properties);
    }

    /// <summary>Declares properties from a template at materialization (cloned per instance by the caller).</summary>
    public void AddProperties(IEnumerable<Property> properties)
    {
        _properties.AddRange(properties);
    }

    public T GetValue<T>(Property<T> property) =>
        property.Value;

    public void SetValue<T>(Property<T> property, T value)
    {
        if (property.IsReadOnly)
            throw new InvalidOperationException($"Property '{property.Name}' is read-only.");

        if (!property.IsValidValue(value))
            throw new ArgumentException($"Invalid value for property '{property.Name}'.");

        property.Value = value;
        NotifyChanged(property);
    }

    public void ClearValue(Property property)
    {
        property.BoxedValue = null;
        NotifyChanged(property);
    }

    public object? GetValue(string name)
    {
        var property = FindProperty(name)
            ?? throw new KeyNotFoundException($"Property '{name}' not found.");
        return property.BoxedValue ?? property.GetUnsetValue();
    }

    /// <summary>
    /// Non-throwing lookup of a property-table value: true with the effective value
    /// (instance value or default) when the property exists, false otherwise. Used by
    /// generic consumers (e.g. the floor plan) that read config-driven properties by
    /// name from any entity without knowing its concrete type.
    /// </summary>
    public bool TryGetValue(string name, out object? value)
    {
        var property = FindProperty(name);
        if (property is null)
        {
            value = null;
            return false;
        }

        value = property.BoxedValue ?? property.GetUnsetValue();
        return true;
    }

    public void SetValue(string name, object? value)
    {
        var property = FindProperty(name)
            ?? throw new KeyNotFoundException($"Property '{name}' not found.");
        property.BoxedValue = value;
        NotifyChanged(property);
    }

    public void ClearValue(string name)
    {
        var property = FindProperty(name);
        if (property is not null)
            ClearValue(property);
    }

    public object? this[Property property]
    {
        get => property.BoxedValue ?? property.GetUnsetValue();
        set => property.BoxedValue = value;
    }

    public object? this[string name]
    {
        get => GetValue(name);
        set => SetValue(name, value);
    }

    public List<Dictionary<string, object?>> GetSchema() =>
        _properties.Select(p => p.ToSchema()).ToList();

    // ── Behavior components ──────────────────────────────
    // Class-level API (used directly on concrete entities); IComponentSource's
    // default methods cover non-Entity implementers like Home.

    public IList<Component> Components => _components;
    private readonly List<Component> _components = new();

    public void AddComponent(Component component)
    {
        _components.Add(component);
    }

    public void RemoveComponent(Component component)
    {
        _components.Remove(component);
    }

    public T? GetComponent<T>() where T : Component =>
        _components.OfType<T>().FirstOrDefault();

    public List<T> GetComponents<T>() where T : Component =>
        _components.OfType<T>().ToList();

    public bool TryGetComponent<T>(out T result) where T : Component
    {
        var comp = GetComponent<T>();
        if (comp is not null) { result = comp; return true; }
        result = default!;
        return false;
    }

    public Component? GetComponent(Type type) =>
        _components.FirstOrDefault(type.IsInstanceOfType);

    public List<Component> GetComponents(Type type) =>
        _components.Where(type.IsInstanceOfType).ToList();

    public Component? GetComponent(Guid id) =>
        _components.FirstOrDefault(c => c.Id == id);

    // ── Private helpers ──────────────────────────────────

    private Property? FindProperty(string name) =>
        _properties.FirstOrDefault(p => p.Name == name);

    private void NotifyChanged(Property property) =>
        PropertyValueChanged?.Invoke(this, new PropertyValueChangedEventArgs(property, property.BoxedValue ?? property.GetUnsetValue()));
}
