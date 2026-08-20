using Momoka.Home;
using Momoka.Home.Primitives;
using Momoka.Home.Data.Json;
namespace Momoka.Home.Entities;

[JsonTypeName("int")]
public class IntProperty : Property<int>
{
    public IntProperty() { }

    public IntProperty(string name, int defaultValue = 0, string description = "")
        : base(name, defaultValue, description)
    {
    }

    protected override Property<int> CreateCopy(string name, int defaultValue, string description) =>
        new IntProperty(name, defaultValue, description);

    protected override string SchemaTypeName() => "integer";
}
