using Momoka.Home;
using Momoka.Home.Primitives;
namespace Momoka.Home.States;

public class TextureProperty : Property<string>
{
    public TextureProperty(string name, Key templateKey, string defaultValue = "", string description = "")
        : base(name, templateKey, defaultValue, description)
    {
    }

    protected override Property<string> CreateCopy(string name, Key templateKey, string defaultValue, string description) =>
        new TextureProperty(name, templateKey, defaultValue, description);

    protected override string SchemaTypeName() => "texture";
}
