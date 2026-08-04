using Momoka.Home;
using Momoka.Home.Primitives;
namespace Momoka.Home.States;

public class StringProperty : Property<string>
{
    public StringProperty(string name, Key templateKey, string defaultValue = "", string description = "")
        : base(name, templateKey, defaultValue, description)
    {
    }

    protected override Property<string> CreateCopy(string name, Key templateKey, string defaultValue, string description) =>
        new StringProperty(name, templateKey, defaultValue, description);

    protected override string SchemaTypeName() => "string";
}
