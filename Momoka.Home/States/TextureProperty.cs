using Momoka.Home;
using Momoka.Home.Primitives;
using Momoka.Home.Serialization;
namespace Momoka.Home.States;

[JsonTypeName("texture")]
public class TextureProperty : Property<string>
{
    public TextureProperty(string name, string defaultValue = "", string description = "")
        : base(name, defaultValue, description)
    {
    }

    protected override Property<string> CreateCopy(string name, string defaultValue, string description) =>
        new TextureProperty(name, defaultValue, description);

    protected override string SchemaTypeName() => "texture";
}
