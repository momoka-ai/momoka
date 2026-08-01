namespace Momoka.Home.Models.States;

public class FloatProperty : Property<float>
{
    public FloatProperty(string name, Type ownerType, float defaultValue = 0f, string description = "")
        : base(name, ownerType, defaultValue, description)
    {
    }

    protected override string SchemaTypeName() => "number";
}
