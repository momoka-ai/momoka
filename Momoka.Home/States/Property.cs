using System.Text.Json;
using Momoka.Home;
using Momoka.Home.Primitives;
namespace Momoka.Home.States;

public abstract class Property
{
    public string Name { get; }
    public string Description { get; }
    public abstract Type PropertyType { get; }

    public bool IsReadOnly { get; set; }
    public Func<object?, bool>? ValidateValueCallback { get; set; }

    /// <summary>Optional closed set of valid values (config-driven "literals").</summary>
    public IReadOnlyList<string>? ValidValues { get; set; }

    /// <summary>
    /// Per-instance value (config-driven: each entity owns its properties, so the
    /// value lives on the property itself). Null means "use <see cref="DefaultValue"/>.
    /// </summary>
    public object? Value { get; set; }

    /// <summary>Creates a fresh property with the same definition and value (per-instance materialization).</summary>
    public abstract Property Clone();

    protected Property(string name, string description = "")
    {
        Name = name;
        Description = description;
    }

    public virtual IEnumerable<string>? GetValidValues() => ValidValues;

    public bool IsValidType(object? value)
    {
        if (value is null)
            return !PropertyType.IsValueType
                || Nullable.GetUnderlyingType(PropertyType) is not null;

        return PropertyType.IsAssignableFrom(value.GetType());
    }

    public bool IsValidValue(object? value)
    {
        if (!IsValidType(value))
            return false;

        if (ValidateValueCallback is not null)
            return ValidateValueCallback(value);

        return true;
    }

    public Dictionary<string, object?> ToSchema()
    {
        var schema = new Dictionary<string, object?>
        {
            ["name"] = Name,
            ["type"] = SchemaTypeName(),
            ["default"] = SerializeValue(GetDefaultValue()!),
            ["isReadOnly"] = IsReadOnly
        };

        if (!string.IsNullOrEmpty(Description))
            schema["description"] = Description;

        var validValues = GetValidValues();
        if (validValues is not null)
            schema["validValues"] = validValues.ToList();

        return schema;
    }

    protected virtual string SchemaTypeName() => PropertyType.Name.ToLowerInvariant();

    public static Property Create(
        string name,
        Type propertyType,
        Func<object?, bool>? validateValueCallback = null,
        string description = "")
    {
        var genericType = typeof(Property<>).MakeGenericType(propertyType);
        var defaultValue = propertyType.IsValueType
            ? Activator.CreateInstance(propertyType)
            : null;

        var prop = (Property)Activator.CreateInstance(genericType, name, defaultValue!)!;
        return prop;
    }

    public abstract object? GetDefaultValue();
    public abstract object? SerializeValue(object value);
    public abstract object DeserializeValue(object? raw);
}

public abstract class Property<T> : Property
{
    public override Type PropertyType => typeof(T);
    public T DefaultValue { get; }

    protected Property(string name, T defaultValue, string description = "")
        : base(name, description)
    {
        DefaultValue = defaultValue;
    }

    public override object? GetDefaultValue() => DefaultValue;

    public override object? SerializeValue(object value) => Serialize((T)value);
    public override object DeserializeValue(object? raw) => Deserialize(raw)!;

    public override Property Clone()
    {
        var copy = CreateCopy(Name, DefaultValue, Description);
        copy.ValidateValueCallback = ValidateValueCallback;
        copy.IsReadOnly = IsReadOnly;
        copy.ValidValues = ValidValues;
        copy.Value = Value;
        return copy;
    }

    /// <summary>Constructs a fresh instance of the concrete property subclass (used by <see cref="Clone"/>).</summary>
    protected abstract Property<T> CreateCopy(string name, T defaultValue, string description);

    protected virtual object? Serialize(T value) => value;
    protected virtual T Deserialize(object? raw) =>
        raw is JsonElement je ? JsonSerializer.Deserialize<T>(je.GetRawText())! : (T)raw!;
}
