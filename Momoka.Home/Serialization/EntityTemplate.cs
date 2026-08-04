using Momoka.Home.Components;
using Momoka.Home.Entities;
using Momoka.Home.Primitives;
using Momoka.Home.Shapes;
using Momoka.Home.States;
using Newtonsoft.Json;
namespace Momoka.Home.Serialization;

/// <summary>
/// Typed descriptor of an entity type: the identity (<see cref="Key"/>,
/// <see cref="Typename"/>) plus the limited, known set of content fields (shape,
/// properties, components). Built by <see cref="EntityConfigLoader"/> from a config
/// file — key derived from the path, "typename" resolved against the template
/// registry (inheritance + merge) — and materialized by <see cref="EntityFactory"/>.
/// </summary>
public class EntityTemplate
{
    /// <summary>
    /// The template's type identity — also stamped onto produced entities'
    /// <see cref="Entity.Key"/>. Derived from the config file path, never from JSON.
    /// </summary>
    public Key Key { get; }

    /// <summary>
    /// Registered type this template inherits from, resolved in the template
    /// registry (e.g. "voxelentity", "entity.appliance.air_conditioner").
    /// </summary>
    [JsonProperty("typename"), JsonRequired]
    public string Typename { get; }

    /// <summary>Shape, inherited from the parent template and overridden by this config.</summary>
    [JsonProperty("shape")]
    public Shape? Shape { get; set; }

    /// <summary>Properties, merged from the parent template by name.</summary>
    [JsonProperty("properties")]
    public List<Property>? Properties { get; set; }

    /// <summary>Components (resolution not implemented yet).</summary>
    [JsonProperty("components")]
    public List<Component>? Components { get; set; }

    public EntityTemplate(Key key, string typename)
    {
        Key = key;
        Typename = typename;
    }
}
