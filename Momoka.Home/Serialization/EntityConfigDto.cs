using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
namespace Momoka.Home.Serialization;

/// <summary>
/// Top-level entity config: which registered type to build from ("typename",
/// resolved in the factory registry) plus the content table. The template's key
/// is NOT stored here — it is derived from the file path (folder = namespace,
/// filename = key path).
/// </summary>
public sealed class EntityConfigDto
{
    /// <summary>Registered type name this config builds from (looked up in the factory registry).</summary>
    [JsonProperty("typename")] public string TypeName { get; set; } = "";

    [JsonProperty("version")] public int Version { get; set; } = 1;

    [JsonProperty("content")] public EntityContentDto? Content { get; set; }
}

/// <summary>Shape + properties + components of a config-driven entity.</summary>
public sealed class EntityContentDto
{
    [JsonProperty("shape")] public ShapeDto? Shape { get; set; }

    [JsonProperty("properties")] public List<PropertyDto> Properties { get; set; } = new();

    /// <summary>Component keys (resolution not implemented yet).</summary>
    [JsonProperty("components")] public List<string> Components { get; set; } = new();
}

/// <summary>
/// Declarative shape: the "kind" discriminator (box / line / …) plus kind-specific
/// params captured by <see cref="JsonExtensionData"/> (box size, line start/end…).
/// </summary>
public sealed class ShapeDto
{
    [JsonProperty("kind")] public string Kind { get; set; } = "";

    [JsonExtensionData] public Dictionary<string, JToken> Params { get; set; } = new();
}

/// <summary>Declarative property: key, type, optional initial value, optional closed set of values.</summary>
public sealed class PropertyDto
{
    [JsonProperty("key")] public string Key { get; set; } = "";

    [JsonProperty("type")] public string Type { get; set; } = "";

    [JsonProperty("value")] public JToken? Value { get; set; }

    [JsonProperty("values")] public List<string>? Values { get; set; }

    [JsonProperty("description")] public string? Description { get; set; }
}
