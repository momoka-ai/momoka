using Momoka.Home;
using Momoka.Home.Primitives;
namespace Momoka.Home.States;

public class IntProperty : Property<int>
{
    public IntProperty(string name, int defaultValue = 0, string description = "")
        : base(name, defaultValue, description)
    {
    }

    protected override Property<int> CreateCopy(string name, int defaultValue, string description) =>
        new IntProperty(name, defaultValue, description);

    protected override string SchemaTypeName() => "integer";
}
