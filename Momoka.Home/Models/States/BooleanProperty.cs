namespace Momoka.Home.Models.States;

public class BooleanProperty : Property<bool>
{
    public BooleanProperty(string name, Type ownerType, bool defaultValue = false, string description = "")
        : base(name, ownerType, defaultValue, description)
    {
    }

    protected override string SchemaTypeName() => "boolean";
}
