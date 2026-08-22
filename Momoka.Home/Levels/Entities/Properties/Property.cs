using Newtonsoft.Json;
namespace Momoka.Home.Levels.Entities.Properties;

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
    /// <summary>
    /// Marks an entity as fixed building fabric (floors, walls, doors, windows) —
    /// immovable and space-defining. Its placement surfaces seed region labels;
    /// portals (doors) are passable when open.
    /// </summary>
    public const string IsImmutable = "is_immutable";

    /// <summary>Current open state of a portal (a door); immutable portals are passable when open.</summary>
    public const string IsOpen = "is_open";

    /// <summary>Marks an entity as see-through (glass, open mesh); non-transparent entities stop hitscan lines.</summary>
    public const string IsTransparent = "is_transparent";

    /// <summary>期望放置表面朝向类别（<see cref="Momoka.Home.Primitives.RotationAlignment"/> 枚举值，
    /// 模板配置；缺省 Any——不限定表面朝向）。</summary>
    public const string RotationAlignment = "rotation_alignment";

    /// <summary>隐藏 Home 实体的档案地址（本地展示；云端账号管理为权威）。</summary>
    public const string Address = "address";

    /// <summary>隐藏 Home 实体的住宅类型（<see cref="Momoka.Home.Levels.LevelType"/>；LevelData.Type 的持久化真相）。</summary>
    public const string LevelType = "unit_type";

    /// <summary>贴图资源 key（重涂 = 设置/清除一个 StringProperty；缺省回落编辑器预设材质）。</summary>
    public const string Texture = "texture";

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
    /// The current value in boxed form (null = unset → <see cref="GetUnsetValue"/>).
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

    /// <summary>The value used when <see cref="BoxedValue"/> is null (the unset value).</summary>
    public abstract object? GetUnsetValue();

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
            ["default"] = GetUnsetValue(),
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
public abstract class Property<T> : Property, ICloneable
{
    public override Type ValueType => typeof(T);

    /// <summary>The default value; <see cref="Value"/> equals it until explicitly set.</summary>
    [JsonIgnore]
    public T UnsetValue { get; } = default!;

    private T _value = default!;

    /// <summary>
    /// Typed per-instance value, seeded with <see cref="UnsetValue"/> and only
    /// replaced by an explicit set — no separate "unset" flag. Maps to the JSON
    /// "value" field. NOTE: in a generic class <c>T?</c> is not
    /// <c>Nullable&lt;T&gt;</c> for value types (Property&lt;bool&gt;.Value is
    /// <c>bool</c>, default <c>false</c>), so the storage is a plain T seeded
    /// with the default.
    /// </summary>
    [JsonProperty("value", NullValueHandling = NullValueHandling.Ignore)]
    public virtual T Value
    {
        get => _value;
        set => _value = value;
    }

    /// <summary>Boxed current value; null is treated as "reset to default".</summary>
    public override object? BoxedValue
    {
        get => _value;
        set => _value = value is null ? UnsetValue : (T)value;
    }

    protected Property() { }

    protected Property(string name, T defaultValue, string description = "")
        : base(name, description)
    {
        UnsetValue = defaultValue;
        _value = defaultValue;
    }

    public override object? GetUnsetValue() => UnsetValue;

    public override Property Clone()
    {
        var copy = CreateCopy(Name, UnsetValue, Description);
        copy.IsReadOnly = IsReadOnly;
        copy.BoxedValue = BoxedValue;
        return copy;
    }

    /// <summary>Constructs a fresh instance of the concrete property subclass (used by <see cref="Clone"/>).</summary>
    protected abstract Property<T> CreateCopy(string name, T defaultValue, string description);

    object ICloneable.Clone()
    {
        return Clone();
    }
}
