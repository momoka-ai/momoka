namespace Momoka.Home.Properties;

/// <summary>
/// Built-in property definitions shared across the model. Config declares them
/// by name; code references them through these constants.
/// </summary>
public static class BuiltinProperty
{
    /// <summary>
    /// Marks an entity's placement surfaces as walking bases for region labeling
    /// (floors, stairs, yard ground). Placement surfaces without it are still
    /// placeable, but never seed a region.
    /// </summary>
    public static readonly BooleanProperty IS_STRUCTURAL = new("is_structural");
}
