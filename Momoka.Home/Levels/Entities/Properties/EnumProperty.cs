using System.Text.Json;
using System.Text.Json.Serialization;
using Momoka.Home.Data.Json;
namespace Momoka.Home.Levels.Entities.Properties;

/// <summary>
/// A property whose value is a CLR enum (config "enum"): the enum type is
/// self-described by <see cref="EnumTypeName"/> and the value is stored by
/// NAME (<see cref="Value"/>) — reordering/inserting enum members never
/// invalidates saved data. The only non-generic member of the property
/// taxonomy, so the registry can address it like any closed kind.
/// </summary>
[JsonTypeName("enum")]
public class EnumProperty : Property
{
    [JsonPropertyName("enum_type")]
    public string EnumTypeName { get; set; } = "";

    [JsonPropertyName("value")]
    public string Value { get; set; } = "";

    private string _default = "";

    public EnumProperty() { }

    private EnumProperty(string name, Type enumType, string value, string description)
        : base(name, description)
    {
        EnumTypeName = enumType.AssemblyQualifiedName!;
        _default = value;
        Value = value;
    }

    /// <summary>Type-safe factory: preserves the generic API without a generic subtype.</summary>
    public static EnumProperty Create<T>(string name, T value, string description = "") where T : Enum
        => new(name, typeof(T), value.ToString() ?? "", description);

    [JsonIgnore]
    public override Type ValueType => EnumType;

    private Type EnumType => Type.GetType(EnumTypeName)
        ?? throw new JsonException($"Unknown enum type '{EnumTypeName}'.");

    [JsonIgnore]
    public override object? BoxedValue
    {
        get => Enum.Parse(EnumType, string.IsNullOrEmpty(Value) ? _default : Value);
        set => Value = value is null ? _default : value.ToString()!;
    }

    public override object? GetUnsetValue() =>
        Enum.Parse(EnumType, string.IsNullOrEmpty(_default) ? Value : _default);

    public override IEnumerable<string> GetValidValues() => Enum.GetNames(EnumType);

    public override Property Clone()
    {
        var copy = new EnumProperty(Name, EnumType, Value, Description);
        copy.IsReadOnly = IsReadOnly;
        return copy;
    }

    public override void OnConfigLoaded()
    {
        if (!string.IsNullOrEmpty(Value) && !Enum.IsDefined(EnumType, Value))
            throw new ArgumentException($"Value '{Value}' is not valid for enum property '{Name}'.");
    }

    protected override string SchemaTypeName() => "enum";
}
