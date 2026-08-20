using Momoka.Home.Entities;
using Momoka.Home.Data.Json;
using Momoka.Home.Entities.Components;
namespace Momoka.Home.Data.Json.Converters;

/// <summary>
/// Serializes a <see cref="Component"/> via <see cref="JsonTypeConverter{T}"/>:
/// the "kind" discriminator is declared by <see cref="JsonTypeNameAttribute"/>
/// and resolved through <see cref="JsonTypeNameRegistry"/>; members bind
/// directly with snake_case naming. Uses "kind" (not "type") so components with
/// a <c>Type</c> member (DataSource/EventSource) don't collide with the
/// discriminator.
/// </summary>
public class JsonComponentConverter : JsonTypeConverter<Component>
{
    protected override string Discriminator => "kind";
}
