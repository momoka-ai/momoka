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
            "boolean" => new BooleanProperty(dto.Key, templateKey, dto.Value?.Value<bool>() ?? false),
            "int" => new IntProperty(dto.Key, templateKey, dto.Value?.Value<int>() ?? 0),
            "float" => new FloatProperty(dto.Key, templateKey, dto.Value?.Value<float>() ?? 0f),
            "string" => new StringProperty(dto.Key, templateKey, dto.Value?.Value<string>() ?? ""),
            "texture" => new TextureProperty(dto.Key, templateKey, dto.Value?.Value<string>() ?? ""),
            "literals" => new StringProperty(dto.Key, templateKey, dto.Value?.Value<string>() ?? "")
            {
                ValidValues = dto.Values
            },
            _ => throw new NotSupportedException($"Unknown property type '{dto.Type}'.")
        };
    }
}
