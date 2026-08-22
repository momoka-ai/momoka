using System.Text.Json.Serialization;
using Momoka.Home.Levels.Volumes;
using Momoka.Home.Primitives;
using Momoka.Home.Levels.Entities;
using Momoka.Home.Data.Json;
using Momoka.Home.Levels.Entities.Properties;
namespace Momoka.Home.Levels.Entities;

/// <summary>
/// Typed descriptor of an entity template: the identity (<see cref="Key"/>) plus
/// the composed content (shape, properties, components). Loaded by
/// <see cref="EntityTemplateFactory"/> from a config file — key derived from the
/// path, "extends" resolved against the registry as mixin composition — and
/// materialized into an <see cref="Entity"/>.
/// </summary>
public class EntityTemplate
{
    /// <summary>
    /// The template's identity — also stamped onto produced entities'
    /// <see cref="Entity.Key"/>. Derived from the config file path, set after
    /// deserialization (never read from JSON).
    /// </summary>
    [JsonIgnore]
    public Key Key { get; set; }

    /// <summary>
    /// Templates this one is composed from (mixins), resolved in the registry and
    /// merged in array order — later entries override earlier ones by name; this
    /// config's own fields override everything. Pure data, so no diamond problem.
    /// </summary>
    [JsonPropertyName("extends")]
    public List<string> Extends { get; set; } = new();

    /// <summary>Volume, inherited from the extended templates and overridden by this config.</summary>
    [JsonPropertyName("shape")]
    public Volume? Volume { get; set; }

    /// <summary>Properties, merged from the extended templates by name.</summary>
    [JsonPropertyName("properties")]
    public List<Property>? Properties { get; set; }

    /// <summary>Component type keys (resolution into instances comes later).</summary>
    [JsonPropertyName("components")]
    public List<string>? Components { get; set; }
}
