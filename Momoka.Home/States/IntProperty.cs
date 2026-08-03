using Momoka.Home;
using Momoka.Home.Primitives;
namespace Momoka.Home.States;

public class IntProperty : Property<int>
{
    public IntProperty(string name, Key templateKey, int defaultValue = 0, string description = "")
        : base(name, templateKey, defaultValue, description)
    {
    }

    protected override string SchemaTypeName() => "integer";
}
