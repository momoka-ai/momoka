using Momoka.Home.Data.Json.Converters;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;
namespace Momoka.Home.Data;

/// <summary>Central application settings.</summary>
public static class Settings
{
    /// <summary>
    /// The canonical JSON settings for direct Momoka serialization: snake_case,
    /// indented, snake_case enums, plus the geometry/property/component/key
    /// converters — the single settings object for entities, residences and the
    /// <c>Residence.json</c> save file.
    /// </summary>
    public static readonly JsonSerializerSettings JsonSerialization = new()
    {
        ContractResolver = new DefaultContractResolver { NamingStrategy = new SnakeCaseNamingStrategy() },
        Formatting = Formatting.Indented,
        Converters =
        {
            new JsonGeometryConverter(),
            new JsonPropertyConverter(),
            new JsonComponentConverter(),
            new JsonKeyConverter(),
            new StringEnumConverter { NamingStrategy = new SnakeCaseNamingStrategy() },
        },
    };
}
