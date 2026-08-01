namespace Momoka.Home.Models.States;

public class StringProperty : Property<string>
{
    public StringProperty(string name, Type ownerType, string defaultValue = "", string description = "")
        : base(name, ownerType, defaultValue, description)
    {
    }

    protected override string SchemaTypeName() => "string";
}
