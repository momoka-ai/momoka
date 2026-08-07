using Momoka.Home.Geometry;
namespace Momoka.Home.Storage;

/// <summary>
/// Serializes <see cref="Volume"/> (and the 2D <see cref="Shape"/> footprints they
/// embed) via <see cref="JsonTypeConverter{T}"/>: the "kind" discriminator is
/// declared by <see cref="JsonTypeNameAttribute"/> and resolved through
/// <see cref="JsonTypeNameRegistry"/>; all params bind directly to the type's
/// properties with snake_case naming. Adding a shape = one [JsonTypeName]
/// attribute on the class, nothing else.
/// </summary>
public class JsonGeometryConverter : JsonTypeConverter<Volume>
{
    protected override string Discriminator => "kind";

    public override bool CanConvert(Type objectType) =>
        typeof(Volume).IsAssignableFrom(objectType) || typeof(Shape).IsAssignableFrom(objectType);

    protected override Type ResolveTargetType(string kind, Type declaredType) =>
        typeof(Volume).IsAssignableFrom(declaredType)
            ? JsonTypeNameRegistry.TypeOf<Volume>(kind)
            : JsonTypeNameRegistry.TypeOf<Shape>(kind);

    protected override string NameOf(Type type) =>
        typeof(Volume).IsAssignableFrom(type)
            ? JsonTypeNameRegistry.NameOf<Volume>(type)
            : JsonTypeNameRegistry.NameOf<Shape>(type);
}
