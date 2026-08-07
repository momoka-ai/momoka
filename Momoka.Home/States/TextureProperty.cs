using Momoka.Home;
using Momoka.Home.Primitives;
using Momoka.Home.Storage;
namespace Momoka.Home.States;

/// <summary>
/// A string-typed property whose value is a texture/material path (e.g.
/// <c>texture.midea.air_conditioner.ac_1523</c>). Part of the config vocabulary —
/// the <c>texture</c> kind in property tables.
/// </summary>
[JsonTypeName("texture")]
public class TextureProperty : Property<string>
{
    public TextureProperty() { }

    public TextureProperty(string name, string defaultValue = "", string description = "")
        : base(name, defaultValue, description)
    {
    }

    protected override Property<string> CreateCopy(string name, string defaultValue, string description) =>
        new TextureProperty(name, defaultValue, description);

    protected override string SchemaTypeName() => "texture";
}
