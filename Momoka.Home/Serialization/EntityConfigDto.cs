using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
namespace Momoka.Home.Serialization;

/// <summary>
/// Flat JSON model of an entity config file, mirroring <see cref="EntityTemplate"/>'s
/// fields. The template key is NOT stored here — it is derived from the file path
/// (folder = namespace, filename = key path). Deserialized by the loader, which
/// then resolves the "class" and merges content into an <see cref="EntityTemplate"/>.
/// </summary>
public sealed class EntityConfigDto
{
    /// <summary>Registered type this config inherits from (resolved in the template registry).</summary>
    [JsonProperty("class"), JsonRequired]
    public string Class { get; set; } = "";

    [JsonProperty("shape")] public ShapeDto? Shape { get; set; }

    [JsonProperty("properties")] public List<PropertyDto>? Properties { get; set; }

    /// <summary>Component keys (resolution not implemented yet).</summary>
    [JsonProperty("components")] public List<string>? Components { get; set; }
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
