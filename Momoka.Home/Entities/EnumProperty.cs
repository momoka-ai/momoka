using Momoka.Home;
using Momoka.Home.Primitives;
namespace Momoka.Home.Entities;

public class EnumProperty<T> : Property<T>
    where T : Enum
{
    public EnumProperty(string name, T defaultValue, string description = "")
        : base(name, defaultValue, description)
    {
    }

    protected override Property<T> CreateCopy(string name, T defaultValue, string description) =>
        new EnumProperty<T>(name, defaultValue, description);

    public override IEnumerable<string> GetValidValues() =>
        Enum.GetNames(typeof(T));

    protected override string SchemaTypeName() => "enum";
}
