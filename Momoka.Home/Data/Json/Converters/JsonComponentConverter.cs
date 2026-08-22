using Momoka.Home.Levels.Entities;
using Momoka.Home.Data.Json;
using Momoka.Home.Levels.Entities.Components;
namespace Momoka.Home.Data.Json.Converters;

/// <summary>
/// Serializes a <see cref="Component"/> via <see cref="JsonTypeConverter{T}"/>:
/// <c>{ "kind": …, "data": { … } }</c> envelope with members delegated to stock
/// Json.NET. The "kind" discriminator is declared by <see cref="JsonTypeNameAttribute"/>
/// and resolved through <see cref="JsonTypeNameRegistry"/>. Uses "kind" (not
/// "type") so components with a <c>Type</c> member (DataSource/EventSource)
/// don't collide with the discriminator.
/// </summary>
public class JsonComponentConverter : JsonTypeConverter<Component>
{
    protected override string Discriminator => "kind";
}
