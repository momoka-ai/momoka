using Momoka.Home;
using Momoka.Home.Primitives;
using Momoka.Home.Serialization;
namespace Momoka.Home.States;

[JsonTypeName("float")]
public class FloatProperty : Property<float>
{

    public FloatProperty() { }

    public FloatProperty(string name, float defaultValue = 0f, string description = "")
        : base(name, defaultValue, description)
    {
    }

    protected override Property<float> CreateCopy(string name, float defaultValue, string description) =>
        new FloatProperty(name, defaultValue, description);

    protected override string SchemaTypeName() => "number";
}
