using Momoka.Home.Entities;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
namespace Momoka.Home.Storage;

/// <summary>
/// JSON codec for the entity list (<c>Entities.json</c>): serializes each
/// entity's full instance state — key, coords, volume, per-instance properties
/// and components (incl. placement surfaces) — so a save is self-contained.
/// </summary>
public static class EntitiesCodec
{
    internal static readonly JsonSerializerSettings Settings = new()
    {
        ContractResolver = new DefaultContractResolver { NamingStrategy = new SnakeCaseNamingStrategy() },
        Formatting = Formatting.Indented,
        Converters =
        {
            new JsonGeometryConverter(),
            new JsonPropertyConverter(),
            new JsonComponentConverter(),
            new JsonKeyConverter(),
        },
    };

    public static string Serialize(IEnumerable<Entity> entities) =>
        JsonConvert.SerializeObject(new EntitiesFile { Entities = entities.ToList() }, Settings);

    public static List<Entity> Deserialize(string json) =>
        JsonConvert.DeserializeObject<EntitiesFile>(json, Settings)!.Entities;
}

/// <summary>File wrapper for <c>Entities.json</c> (version + flat entity list).</summary>
public sealed class EntitiesFile
{
    public int Version { get; set; } = 1;
    public List<Entity> Entities { get; set; } = new();
}
