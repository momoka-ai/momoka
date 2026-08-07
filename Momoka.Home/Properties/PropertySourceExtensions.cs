namespace Momoka.Home.Properties;

/// <summary>
/// Property-table helpers for <see cref="IPropertySource"/> — declared once as
/// extension methods so implementers only expose the property list and the
/// change event.
/// </summary>
public static class PropertySourceExtensions
{
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

    public static T GetValue<T>(this IPropertySource source, Property<T> property) =>
        property.Value;

    public static void SetValue<T>(this IPropertySource source, Property<T> property, T value)
    {
        if (property.IsReadOnly)
            throw new InvalidOperationException($"Property '{property.Name}' is read-only.");

        if (!property.IsValidValue(value))
            throw new ArgumentException($"Invalid value for property '{property.Name}'.");

        property.Value = value;
        source.NotifyPropertyChanged(property, value);
    }

    public static void ClearValue(this IPropertySource source, Property property)
    {
        property.BoxedValue = null;
        source.NotifyPropertyChanged(property, property.GetUnsetValue());
    }

    public static object? GetValue(this IPropertySource source, string name)
    {
        var property = FindProperty(source, name)
            ?? throw new KeyNotFoundException($"Property '{name}' not found.");
        return property.BoxedValue ?? property.GetUnsetValue();
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
            source.ClearValue(property);
    }

    public static List<Dictionary<string, object?>> GetSchema(this IPropertySource source) =>
        source.Properties.Select(p => p.ToSchema()).ToList();

    private static Property? FindProperty(IPropertySource source, string name) =>
        source.Properties.FirstOrDefault(p => p.Name == name);
}
