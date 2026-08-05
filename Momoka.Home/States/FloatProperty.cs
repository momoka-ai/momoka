using Momoka.Home;
using Momoka.Home.Primitives;
namespace Momoka.Home.States;

public class FloatProperty : Property<float>
{
    public FloatProperty(string name, float defaultValue = 0f, string description = "")
        : base(name, defaultValue, description)
    {
    }

    protected override Property<float> CreateCopy(string name, float defaultValue, string description) =>
        new FloatProperty(name, defaultValue, description);

    protected override string SchemaTypeName() => "number";
}
