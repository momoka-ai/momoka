using Momoka.Home;
using Momoka.Home.Primitives;
namespace Momoka.Home.States;

public class BooleanProperty : Property<bool>
{
    public BooleanProperty(string name, Key templateKey, bool defaultValue = false, string description = "")
        : base(name, templateKey, defaultValue, description)
    {
    }

    protected override Property<bool> CreateCopy(string name, Key templateKey, bool defaultValue, string description) =>
        new BooleanProperty(name, templateKey, defaultValue, description);

    protected override string SchemaTypeName() => "boolean";
}
