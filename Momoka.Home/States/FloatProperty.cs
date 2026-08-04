using Momoka.Home;
using Momoka.Home.Primitives;
namespace Momoka.Home.States;

public class FloatProperty : Property<float>
{
    public FloatProperty(string name, Key templateKey, float defaultValue = 0f, string description = "")
        : base(name, templateKey, defaultValue, description)
    {
    }

    protected override Property<float> CreateCopy(string name, Key templateKey, float defaultValue, string description) =>
        new FloatProperty(name, templateKey, defaultValue, description);

    protected override string SchemaTypeName() => "number";
}
