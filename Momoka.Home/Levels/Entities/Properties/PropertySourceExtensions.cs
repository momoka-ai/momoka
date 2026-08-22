namespace Momoka.Home.Levels.Entities.Properties;

/// <summary>
/// Property-table helpers for <see cref="IPropertySource"/> — name-first:
/// implementers only expose the property list and the change event, and every
/// lookup/mutation goes through the property name (builtins are string
/// constants in <see cref="Property"/>).
/// </summary>
public static class PropertySourceExtensions
{
    /// <summary>
    /// True when <paramref name="source"/> is fixed building fabric (floor, wall,
    /// door, window…) — immovable and space-defining; such cells bound regions.
    /// </summary>
    public static bool IsImmutable(this IPropertySource? source) =>
        source is not null && source.GetValue<bool>(Property.IsImmutable);

    public static void AddProperty(this IPropertySource source, params Property[] properties)
    {
        foreach (var property in properties)
            source.Properties.Add(property);
    }

    public static void AddProperties(this IPropertySource source, IEnumerable<Property> properties)
    {
        foreach (var property in properties)
            source.Properties.Add(property);
    }

    public static object? GetValue(this IPropertySource source, string name)
    {
        var property = FindProperty(source, name)
            ?? throw new KeyNotFoundException($"Property '{name}' not found.");
        return property.BoxedValue ?? property.GetUnsetValue();
    }

    /// <summary>Typed lookup by name; default when absent or type-mismatched.</summary>
    public static T GetValue<T>(this IPropertySource source, string name)
    {
        var p = FindProperty(source, name);
        if (p?.ValueType.Equals(typeof(T)) ?? false)
            return ((Property<T>)p).Value;
        return default!;
    }

    /// <summary>
    /// Non-throwing lookup of a property-table value: true with the effective value
    /// (instance value or default) when the property exists, false otherwise. Used by
    /// generic consumers (e.g. the region builder) that read config-driven properties
    /// by name from any entity without knowing its concrete type.
    /// </summary>
    public static bool TryGetValue(this IPropertySource source, string name, out object? value)
    {
        var property = FindProperty(source, name);
        if (property is null)
        {
            value = null;
            return false;
        }

        value = property.BoxedValue ?? property.GetUnsetValue();
        return true;
    }

    public static void SetValue(this IPropertySource source, string name, object? value)
    {
        var property = FindProperty(source, name)
            ?? throw new KeyNotFoundException($"Property '{name}' not found.");
        property.BoxedValue = value;
        source.NotifyPropertyChanged(property, value);
    }

    public static void ClearValue(this IPropertySource source, string name)
    {
        var property = FindProperty(source, name);
        if (property is not null)
        {
            property.BoxedValue = null;
            source.NotifyPropertyChanged(property, property.GetUnsetValue());
        }
    }

    public static List<Dictionary<string, object?>> GetSchema(this IPropertySource source) =>
        source.Properties.Select(p => p.ToSchema()).ToList();

    private static Property? FindProperty(IPropertySource source, string name) =>
        source.Properties.FirstOrDefault(p => p.Name == name);
}
