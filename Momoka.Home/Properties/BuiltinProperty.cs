namespace Momoka.Home.Properties;

/// <summary>
/// Built-in property names shared across the model. Config declares them by
/// name; code references them through these constants and reads values with the
/// name-first <see cref="PropertySourceExtensions"/> API.
/// </summary>
public static class BuiltinProperty
{
    /// <summary>
    /// Marks an entity's placement surfaces as walking bases for region labeling
    /// (floors, stairs, yard ground). Placement surfaces without it are still
    /// placeable, but never seed a region.
    /// </summary>
    public const string IsStructural = "is_structural";
}
