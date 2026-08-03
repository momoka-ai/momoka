using Momoka.Home;
namespace Momoka.Home.States;

public class IntProperty : Property<int>
{
    public IntProperty(string name, Type ownerType, int defaultValue = 0, string description = "")
        : base(name, ownerType, defaultValue, description)
    {
    }

    protected override string SchemaTypeName() => "integer";
}
