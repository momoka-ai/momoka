namespace Momoka.Home.Storage;

/// <summary>
/// Declares the JSON discriminator string ("kind" for geometry, "type" for
/// properties) of a serializable class. The single source of truth for the
/// config vocabulary — consumed by <see cref="JsonTypeNameRegistry"/> and the
/// geometry/property converters.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class JsonTypeNameAttribute : Attribute
{
    public string Name { get; }

    public JsonTypeNameAttribute(string name) => Name = name;
}
