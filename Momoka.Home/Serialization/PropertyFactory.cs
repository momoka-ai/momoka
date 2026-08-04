using Momoka.Home.Primitives;
using Momoka.Home.States;
using Newtonsoft.Json.Linq;
namespace Momoka.Home.Serialization;

/// <summary>
/// Builds a <see cref="Property"/> from its declarative <see cref="PropertyDto"/>.
/// The "type" string selects the property class; the template's <see cref="Key"/>
/// replaces the C# owner type as the property's origin tag.
/// </summary>
public static class PropertyFactory
{
    public static Property Create(PropertyDto dto, Key templateKey)
    {
        ArgumentNullException.ThrowIfNull(dto);
        return dto.Type switch
        {
            "boolean" => new BooleanProperty(dto.Key, templateKey) { Value = dto.Value?.Value<bool>() },
            "int" => new IntProperty(dto.Key, templateKey) { Value = dto.Value?.Value<int>() },
            "float" => new FloatProperty(dto.Key, templateKey) { Value = dto.Value?.Value<float>() },
            "string" => new StringProperty(dto.Key, templateKey) { Value = dto.Value?.Value<string>() },
            "texture" => new TextureProperty(dto.Key, templateKey) { Value = dto.Value?.Value<string>() },
            "literals" => new StringProperty(dto.Key, templateKey)
            {
                ValidValues = dto.Values,
                Value = dto.Value?.Value<string>()
            },
            _ => throw new NotSupportedException($"Unknown property type '{dto.Type}'.")
        };
    }
}
