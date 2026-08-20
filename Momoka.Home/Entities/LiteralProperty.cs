using Momoka.Home.Data.Json;
using Newtonsoft.Json;
namespace Momoka.Home.Entities;

/// <summary>
/// A string property with a CLOSED set of valid values (config "literals"):
/// assigning anything outside <see cref="ValidValues"/> throws. The
/// value-checking variant of <see cref="StringProperty"/> — the constraint lives
/// here, not on the base.
/// </summary>
[JsonTypeName("literals")]
public class LiteralProperty : StringProperty
{
    /// <summary>The closed set of allowed values (config "values").</summary>
    [JsonProperty("values", NullValueHandling = NullValueHandling.Ignore)]
    public IReadOnlyList<string>? ValidValues { get; set; }

    public LiteralProperty() { }

    public LiteralProperty(string name, string description = "")
        : base(name, description)
    {
    }

    public LiteralProperty(string name, IReadOnlyList<string>? validValues, string defaultValue = "", string description = "")
        : base(name, defaultValue, description)
    {
        ValidValues = validValues;
    }

    [JsonProperty("value", NullValueHandling = NullValueHandling.Ignore)]
    public override string Value
    {
        get => base.Value;
        set
        {
            if (value is not null && ValidValues is not null && !ValidValues.Contains(value))
                throw new ArgumentException($"Value '{value}' is not valid for literal property '{Name}'.");
            base.Value = value!;
        }
    }

    protected override Property<string> CreateCopy(string name, string defaultValue, string description) =>
        new LiteralProperty(name, ValidValues, defaultValue, description);

    public override IEnumerable<string>? GetValidValues() => ValidValues;

    public override void OnConfigLoaded()
    {
        if (Value is not null && ValidValues is not null && !ValidValues.Contains(Value))
            throw new ArgumentException($"Value '{Value}' is not valid for literal property '{Name}'.");
    }

    protected override string SchemaTypeName() => "literals";
}
