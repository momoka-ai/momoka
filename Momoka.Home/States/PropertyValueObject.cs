namespace Momoka.Home.Models.States;

public abstract class PropertyValueObject
{
    private readonly Dictionary<Property, object> _values = new();
    private readonly Dictionary<Property, Delegate> _coercions = new();
    private readonly List<Property> _properties = new();

    public event EventHandler<PropertyValueChangedEventArgs>? PropertyValueChanged;

    protected void AddProperty(params Property[] properties)
    {
        _properties.AddRange(properties);
    }

    // ── Typed access ──────────────────────────────────────

    public T GetValue<T>(Property<T> property)
    {
        return _values.TryGetValue(property, out var v)
            ? (T)v
            : property.DefaultValue;
    }

    public void SetValue<T>(Property<T> property, T value)
    {
        if (property.IsReadOnly)
            throw new InvalidOperationException($"Property '{property.Name}' is read-only.");

        if (!property.IsValidValue(value))
            throw new ArgumentException($"Invalid value for property '{property.Name}'.");

        value = CoerceValue(property, value);
        _values[property] = value!;
        NotifyChanged(property);
    }

    public void ClearValue(Property property)
    {
        _values.Remove(property);
        NotifyChanged(property);
    }

    // ── String-based access ───────────────────────────────

    public object? GetValue(string name)
    {
        var prop = FindProperty(name);
        return prop is not null
            ? _values.TryGetValue(prop, out var v) ? v : prop.GetDefaultValue()
            : throw new KeyNotFoundException($"Property '{name}' not found.");
    }

    public void SetValue(string name, object? value)
    {
        var prop = FindProperty(name)
            ?? throw new KeyNotFoundException($"Property '{name}' not found.");
        _values[prop] = value!;
        NotifyChanged(prop);
    }

    public void ClearValue(string name)
    {
        var prop = FindProperty(name);
        if (prop is not null)
            ClearValue(prop);
    }

    // ── Indexers ──────────────────────────────────────────

    public object? this[Property property]
    {
        get => _values.TryGetValue(property, out var v) ? v : property.GetDefaultValue();
        set => _values[property] = value!;
    }

    public object? this[string name]
    {
        get => GetValue(name);
        set => SetValue(name, value);
    }

    // ── Coercion ──────────────────────────────────────────

    public void CoerceValue<T>(Property<T> property, Func<T, T> coercion)
    {
        _coercions[property] = coercion;
    }

    // ── Schema ────────────────────────────────────────────

    public List<Dictionary<string, object?>> GetSchema()
    {
        var result = new List<Dictionary<string, object?>>();
        foreach (var prop in _properties)
        {
            result.Add(prop.ToSchema());
        }
        return result;
    }

    // ── Serialization ─────────────────────────────────────

    public Dictionary<string, object?> ToDictionary()
    {
        var result = new Dictionary<string, object?>();
        foreach (var prop in _properties)
        {
            var value = _values.TryGetValue(prop, out var v) ? v : prop.GetDefaultValue()!;
            result[prop.Name] = prop.SerializeValue(value);
        }
        return result;
    }

    public void Deserialize(Dictionary<string, object?> data)
    {
        foreach (var prop in _properties)
        {
            if (data.TryGetValue(prop.Name, out var raw))
            {
                _values[prop] = prop.DeserializeValue(raw);
            }
        }
    }

    // ── Private helpers ───────────────────────────────────

    private Property? FindProperty(string name)
    {
        foreach (var prop in _properties)
        {
            if (prop.Name == name)
                return prop;
        }
        return null;
    }

    private T CoerceValue<T>(Property<T> property, T value)
    {
        if (_coercions.TryGetValue(property, out var del) && del is Func<T, T> coercion)
            return coercion(value);
        return value;
    }

    private void NotifyChanged(Property property)
    {
        var value = _values.TryGetValue(property, out var v) ? v : property.GetDefaultValue();
        PropertyValueChanged?.Invoke(this, new PropertyValueChangedEventArgs(property, value));
    }
}
