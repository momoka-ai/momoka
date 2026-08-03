namespace Momoka.Home;

public class TextureProperty : Property<string>
{
    public TextureProperty(string name, Type ownerType, string defaultValue = "", string description = "")
        : base(name, ownerType, defaultValue, description)
    {
    }

    protected override string SchemaTypeName() => "texture";
}
