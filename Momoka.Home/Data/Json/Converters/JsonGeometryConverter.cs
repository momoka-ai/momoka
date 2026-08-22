using Momoka.Home.Geometry;
using Momoka.Home.Data.Json;
namespace Momoka.Home.Data.Json.Converters;

/// <summary>
/// Serializes <see cref="Volume"/> via <see cref="JsonTypeConverter{T}"/>: the
/// "kind" discriminator is declared by <see cref="JsonTypeNameAttribute"/> and
/// resolved through <see cref="JsonTypeNameRegistry"/>; all params bind directly
/// to the type's properties with snake_case naming. Adding a volume = one
/// [JsonTypeName] attribute on the class, nothing else.
/// </summary>
public class JsonGeometryConverter : JsonTypeConverter<Volume>
{
    protected override string Discriminator => "kind";

    public override bool CanConvert(Type objectType) =>
        typeof(Volume).IsAssignableFrom(objectType);

    protected override Type ResolveTargetType(string kind, Type declaredType) =>
        JsonTypeNameRegistry.TypeOf<Volume>(kind);

    protected override string NameOf(Type type) =>
        JsonTypeNameRegistry.NameOf<Volume>(type);
}
