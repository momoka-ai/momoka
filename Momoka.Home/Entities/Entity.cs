using Momoka.Home;
using Momoka.Home.Components;
using Momoka.Home.Primitives;
using Momoka.Home.Shapes;
using Momoka.Home.States;
namespace Momoka.Home.Entities;

/// <summary>
/// Base for everything that carries behavior components and per-instance
/// properties. Properties are declared per instance (config-driven: entities get
/// cloned properties from their template at materialization); each
/// <see cref="Property"/> holds its own <see cref="Property.Value"/>.
/// </summary>
public abstract class Entity : IComponentSource
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Key Key { get; init; }

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
        (T)(property.Value ?? property.DefaultValue)!;

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
        property.Value = null;
        NotifyChanged(property);
    }

    public object? GetValue(string name)
    {
        var property = FindProperty(name)
            ?? throw new KeyNotFoundException($"Property '{name}' not found.");
        return property.Value ?? property.GetDefaultValue();
    }

    public void SetValue(string name, object? value)
    {
        var property = FindProperty(name)
            ?? throw new KeyNotFoundException($"Property '{name}' not found.");
        property.Value = value;
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
        get => property.Value ?? property.GetDefaultValue();
        set => property.Value = value;
    }

    public object? this[string name]
    {
        get => GetValue(name);
        set => SetValue(name, value);
    }

    public List<Dictionary<string, object?>> GetSchema() =>
        _properties.Select(p => p.ToSchema()).ToList();

    public Dictionary<string, object?> ToDictionary()
    {
        var result = new Dictionary<string, object?>();
        foreach (var property in _properties)
        {
            result[property.Name] = property.SerializeValue(property.Value ?? property.GetDefaultValue()!);
        }
        return result;
    }

    public void Deserialize(Dictionary<string, object?> data)
    {
        foreach (var property in _properties)
        {
            if (data.TryGetValue(property.Name, out var raw))
                property.Value = property.DeserializeValue(raw);
        }
    }

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
        PropertyValueChanged?.Invoke(this, new PropertyValueChangedEventArgs(property, property.Value ?? property.GetDefaultValue()));
}

/// <summary>
/// A spatial entity with coordinates of type <typeparamref name="T"/>. The three
/// built-ins are <see cref="Int2"/> (tiles/materials), <see cref="Int3"/> (voxel
/// content — the config-template type), and <see cref="Float3"/> (continuous
/// living/moving objects, never rasterized). <see cref="Shape"/> carries the
/// body's geometry: meaningful for Int2/Int3, left null for Float3.
/// </summary>
public class Entity<T> : Entity where T : struct
{
    public T Coords { get; set; }

    public Shape Shape { get; set; } = null!;
}
