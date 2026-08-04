using Momoka.Home.Components;
using Momoka.Home.Entities;
using Momoka.Home.Primitives;
using Momoka.Home.Shapes;
using Momoka.Home.States;
using Newtonsoft.Json;
namespace Momoka.Home.Serialization;

/// <summary>
/// Typed descriptor of an entity type: the identity (<see cref="Key"/>,
/// <see cref="Class"/>) plus the limited, known set of content fields (shape,
/// properties, components). Built by <see cref="EntityConfigLoader"/> from a config
/// file — key derived from the path, "class" resolved against the template
/// registry (inheritance + merge) — and materialized by <see cref="EntityFactory"/>.
/// </summary>
public class EntityTemplate
{
    /// <summary>
    /// The template's type identity — also stamped onto produced entities'
    /// <see cref="Entity.Key"/>. Derived from the config file path, set after
    /// deserialization (never read from JSON).
    /// </summary>
    [JsonIgnore]
    public Key Key { get; set; }

    /// <summary>
    /// Registered type this template inherits from, resolved in the template
    /// registry (e.g. "voxelentity", "entity.appliance.air_conditioner").
    /// </summary>
    [JsonProperty("class"), JsonRequired]
    public string Class { get; set; } = "";

    /// <summary>
    /// Target CLR type, inherited from the top-level base template (e.g.
    /// "universal.voxel" → <see cref="VoxelEntity"/>). Never read from JSON —
    /// it is carried down the inheritance chain and used at materialization.
    /// </summary>
    [JsonIgnore]
    public Type? Type { get; set; }

    /// <summary>Shape, inherited from the parent template and overridden by this config.</summary>
    [JsonProperty("shape")]
    public Shape? Shape { get; set; }

    /// <summary>Properties, merged from the parent template by name.</summary>
    [JsonProperty("properties")]
    public List<Property>? Properties { get; set; }

    /// <summary>Components (resolution not implemented yet).</summary>
    [JsonProperty("components")]
    public List<Component>? Components { get; set; }
}
