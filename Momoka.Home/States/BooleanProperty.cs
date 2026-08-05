using Momoka.Home;
using Momoka.Home.Primitives;
namespace Momoka.Home.States;

public class BooleanProperty : Property<bool>
{
    public BooleanProperty(string name, bool defaultValue = false, string description = "")
        : base(name, defaultValue, description)
    {
    }

    protected override Property<bool> CreateCopy(string name, bool defaultValue, string description) =>
        new BooleanProperty(name, defaultValue, description);

    protected override string SchemaTypeName() => "boolean";
}
