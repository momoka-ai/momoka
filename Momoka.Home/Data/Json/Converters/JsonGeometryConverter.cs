using Momoka.Home.Levels.Volumes;
using Momoka.Home.Data.Json;
namespace Momoka.Home.Data.Json.Converters;

/// <summary>
/// Serializes a <see cref="Volume"/> via <see cref="JsonTypeConverter{T}"/>:
/// <c>{ "kind": …, "data": { … } }</c> envelope with members delegated to stock
/// Json.NET. The "kind" discriminator is declared by <see cref="JsonTypeNameAttribute"/>
/// and resolved through <see cref="JsonTypeNameRegistry"/>. Adding a volume =
/// one <c>[JsonTypeName]</c> attribute on the class, nothing else.
/// </summary>
public class JsonGeometryConverter : JsonTypeConverter<Volume>
{
    protected override string Discriminator => "kind";
}
