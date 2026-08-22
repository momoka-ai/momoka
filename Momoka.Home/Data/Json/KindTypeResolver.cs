using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using Momoka.Home.Levels.Entities;
using Momoka.Home.Levels.Entities.Components;
using Momoka.Home.Levels.Entities.Properties;
using Momoka.Home.Levels.Volumes;
namespace Momoka.Home.Data.Json;

/// <summary>
/// Drives System.Text.Json polymorphic serialization from the
/// <see cref="JsonTypeNameRegistry"/>: Volume and Component share the "kind"
/// discriminator, Property uses "type" — same vocabulary as the config files.
/// Undeclared derived types fail loudly (<see cref="JsonUnknownDerivedTypeHandling.FailSerialization"/>),
/// keeping the type surface registry-controlled.
/// </summary>
public sealed class KindTypeResolver : DefaultJsonTypeInfoResolver
{
    private static readonly Type[] Families = [typeof(Volume), typeof(Component), typeof(Property)];

    public override JsonTypeInfo GetTypeInfo(Type type, JsonSerializerOptions options)
    {
        var info = base.GetTypeInfo(type, options);
        foreach (var family in Families)
        {
            if (type != family)
                continue;

            info.PolymorphismOptions = new JsonPolymorphismOptions
            {
                TypeDiscriminatorPropertyName = family == typeof(Property) ? "type" : "kind",
                UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FailSerialization,
            };
            foreach (var (name, derived) in RegisteredTypes(family))
                info.PolymorphismOptions.DerivedTypes.Add(new JsonDerivedType(derived, name));
            break;
        }
        return info;
    }

    private static IEnumerable<KeyValuePair<string, Type>> RegisteredTypes(Type family) =>
        family == typeof(Volume) ? JsonTypeNameRegistry.All<Volume>()
        : family == typeof(Component) ? JsonTypeNameRegistry.All<Component>()
        : JsonTypeNameRegistry.All<Property>();
}
