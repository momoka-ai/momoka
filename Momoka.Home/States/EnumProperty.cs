using Momoka.Home;
using Momoka.Home.Primitives;
namespace Momoka.Home.States;

public class EnumProperty<T> : Property<T>
    where T : Enum
{
    public EnumProperty(string name, Key templateKey, T defaultValue, string description = "")
        : base(name, templateKey, defaultValue, description)
    {
    }

    protected override Property<T> CreateCopy(string name, Key templateKey, T defaultValue, string description) =>
        new EnumProperty<T>(name, templateKey, defaultValue, description);

    public override IEnumerable<string> GetValidValues() =>
        Enum.GetNames(typeof(T));

    protected override string SchemaTypeName() => "enum";

    protected override object? Serialize(T value) => value.ToString();

    protected override T Deserialize(object? raw) =>
        raw is string s ? (T)Enum.Parse(typeof(T), s) : (T)raw!;
}
