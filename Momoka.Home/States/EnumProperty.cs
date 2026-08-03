namespace Momoka.Home;

public class EnumProperty<T> : Property<T>
    where T : Enum
{
    public EnumProperty(string name, Type ownerType, T defaultValue, string description = "")
        : base(name, ownerType, defaultValue, description)
    {
    }

    public override IEnumerable<string> GetValidValues() =>
        Enum.GetNames(typeof(T));

    protected override string SchemaTypeName() => "enum";

    protected override object? Serialize(T value) => value.ToString();

    protected override T Deserialize(object? raw) =>
        raw is string s ? (T)Enum.Parse(typeof(T), s) : (T)raw!;
}
