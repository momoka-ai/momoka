using System.Text.Json;
using System.Text.Json.Serialization;
using Momoka.Home.Data.Json;
using Momoka.Home.Data.Json.Converters;
namespace Momoka.Home.Data;

/// <summary>Central application settings.</summary>
public static class Settings
{
    /// <summary>
    /// The canonical JSON options for direct Momoka serialization: snake_case,
    /// indented, snake_case enums, registry-driven polymorphism (via
    /// <see cref="KindTypeResolver"/>) plus the Key converter — the single
    /// options object for entities, residences and the save file.
    /// </summary>
    public static readonly JsonSerializerOptions JsonSerialization = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = true,
        TypeInfoResolver = new KindTypeResolver(),
        PreferredObjectCreationHandling = JsonObjectCreationHandling.Populate,
        Converters =
        {
            new JsonKeyConverter(),
            new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower),
        },
    };
}
