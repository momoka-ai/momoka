using Newtonsoft.Json;
namespace Momoka.Home.States;

/// <summary>
/// A property: a named, typed, per-instance value (config-driven). The abstract
/// base carries identity and the value TYPE only; concrete subclasses own their
/// TYPED storage (see <see cref="Property{T}.Value"/>). <see cref="BoxedValue"/>
/// is the uniform boxed contract for generic consumers (lookup by name, config
/// materialization). Values that need a closed range declare it themselves
/// (e.g. <see cref="LiteralProperty"/>) — there is no universal value list on
/// the base, and no validation callback: invalid assignments are the storage
/// type's own job.
/// </summary>
public abstract class Property
{
    /// <summary>Identity of the property; maps to the JSON "key" field.</summary>
    [JsonProperty("key")]
    public string Name { get; set; } = "";

    [JsonIgnore]
    public string Description { get; set; } = "";

    [JsonIgnore]
    public bool IsReadOnly { get; set; }

    /// <summary>CLR type of the value carried by this property.</summary>
    [JsonIgnore]
    public abstract Type ValueType { get; }

    /// <summary>
    /// The current value in boxed form (null = unset → <see cref="GetDefaultValue"/>).
    /// Subclasses store it TYPED; this uniform accessor serves name-based consumers.
    /// </summary>
    [JsonIgnore]
    public abstract object? BoxedValue { get; set; }

    protected Property() { }

    protected Property(string name, string description = "")
    {
        Name = name;
        Description = description;
    }

    /// <summary>Creates a fresh property with the same definition and value (per-instance materialization).</summary>
    public abstract Property Clone();

    /// <summary>The default value (used when <see cref="BoxedValue"/> is null).</summary>
    public abstract object? GetDefaultValue();

    /// <summary>True when the value is null or of the property's CLR type.</summary>
    public bool IsValidValue(object? value) =>
        value is null || ValueType.IsInstanceOfType(value);

    /// <summary>Optional closed set of valid values, for schema output (literals, enums).</summary>
    public virtual IEnumerable<string>? GetValidValues() => null;

    /// <summary>
    /// Called after config-driven deserialization has populated every member.
    /// Subclasses with cross-field invariants (e.g. a literal whose value must
    /// belong to its closed set) validate here, since member order is not
    /// guaranteed during binding.
    /// </summary>
    public virtual void OnConfigLoaded() { }

    public Dictionary<string, object?> ToSchema()
    {
        var schema = new Dictionary<string, object?>
        {
            ["name"] = Name,
            ["type"] = SchemaTypeName(),
            ["default"] = GetDefaultValue(),
            ["isReadOnly"] = IsReadOnly
        };

        if (!string.IsNullOrEmpty(Description))
            schema["description"] = Description;

        var validValues = GetValidValues();
        if (validValues is not null)
            schema["validValues"] = validValues.ToList();

        return schema;
    }

    protected virtual string SchemaTypeName() => ValueType.Name.ToLowerInvariant();
}

/// <summary>
/// A property whose value is stored TYPED as <typeparamref name="T"/>. The typed
/// <see cref="Value"/> maps to the JSON "value" field, so direct deserialization
/// converts to the concrete CLR type (never a JToken). Subclasses add their own
/// storage and constraints (a closed literal set, an enum, …).
/// </summary>
public abstract class Property<T> : Property
{
    public override Type ValueType => typeof(T);

    /// <summary>The default value, used until <see cref="Value"/> is explicitly set.</summary>
    [JsonIgnore]
    public T DefaultValue { get; } = default!;

    private T _value = default!;
    private bool _isSet;

    /// <summary>
    /// Typed per-instance value; returns <see cref="DefaultValue"/> until explicitly
    /// set (so generic consumers never see an unset state). Maps to the JSON
    /// "value" field. NOTE: in a generic class <c>T?</c> is not <c>Nullable&lt;T&gt;</c>
    /// for value types, so set-ness is tracked separately via <see cref="BoxedValue"/>.
    /// </summary>
    [JsonProperty("value", NullValueHandling = NullValueHandling.Ignore)]
    public virtual T Value
    {
        get => _isSet ? _value : DefaultValue;
        set
        {
            _value = value;
            _isSet = true;
        }
    }

    /// <summary>Boxed current value; null = unset (→ <see cref="DefaultValue"/>).</summary>
    public override object? BoxedValue
    {
        get => _isSet ? _value : null;
        set
        {
            _isSet = value is not null;
            _value = value is null ? default! : (T)value;
        }
    }

    protected Property() { }

    protected Property(string name, T defaultValue, string description = "")
        : base(name, description)
    {
        DefaultValue = defaultValue;
    }

    public override object? GetDefaultValue() => DefaultValue;

    public override Property Clone()
    {
        var copy = CreateCopy(Name, DefaultValue, Description);
        copy.IsReadOnly = IsReadOnly;
        copy.BoxedValue = BoxedValue; // preserves the unset/set state
        return copy;
    }

    /// <summary>Constructs a fresh instance of the concrete property subclass (used by <see cref="Clone"/>).</summary>
    protected abstract Property<T> CreateCopy(string name, T defaultValue, string description);
}
