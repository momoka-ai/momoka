using Momoka.Home;
using Momoka.Home.Primitives;
using Momoka.Home.Serialization;
namespace Momoka.Home.States;

[JsonTypeName("string")]
public class StringProperty : Property<string>
{
    public StringProperty(string name, string defaultValue = "", string description = "")
        : base(name, defaultValue, description)
    {
    }

    protected override Property<string> CreateCopy(string name, string defaultValue, string description) =>
        new StringProperty(name, defaultValue, description);

    protected override string SchemaTypeName() => "string";
}
