using Momoka.Home.Properties;
using Momoka.Home.Data.Json;
namespace Momoka.Home.Data.Json.Converters;

/// <summary>
/// Serializes a <see cref="Property"/> via <see cref="JsonTypeConverter{T}"/>: the
/// "type" discriminator is declared by <see cref="JsonTypeNameAttribute"/> and
/// resolved through <see cref="JsonTypeNameRegistry"/>; "key"/"value"/"values"
/// bind directly with snake_case naming — no per-kind logic. The typed
/// <see cref="Property{T}.Value"/> converts "value" straight to the concrete CLR
/// type (never a JToken).
/// </summary>
public class JsonPropertyConverter : JsonTypeConverter<Property>
{
    protected override string Discriminator => "type";

    protected override void OnLoaded(object value) => ((Property)value).OnConfigLoaded();
}
