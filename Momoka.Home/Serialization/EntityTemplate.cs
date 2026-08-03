using Momoka.Home.Entities;
using Momoka.Home.Primitives;
namespace Momoka.Home.Serialization;

/// <summary>
/// Schema-less descriptor of an entity type, loaded from config (JSON) and
/// materialized into a typed entity by <see cref="EntityFactory"/>. Only the
/// identity (<see cref="Key"/>) and the target type name (<see cref="TypeName"/>)
/// are first-class; everything else lives in the <see cref="Values"/> table
/// (shape, property definitions, components…), interpreted by the factory.
/// </summary>
public class EntityTemplate
{
    /// <summary>
    /// The template's type identity — also stamped onto produced entities'
    /// <see cref="Entity.Key"/>. Reuses the namespaced <see cref="Key"/> primitive.
    /// </summary>
    public Key Key { get; }

    /// <summary>
    /// Target type name; a constructor must be registered for it in
    /// <see cref="EntityFactory"/> (e.g. "entity.appliance.air_conditioner", "wall").
    /// </summary>
    public string TypeName { get; }

    /// <summary>
    /// Everything not captured by <see cref="Key"/> / <see cref="TypeName"/>:
    /// the shape descriptor, property definitions, components… read by the factory.
    /// </summary>
    public IReadOnlyDictionary<string, object?> Values { get; }

    public EntityTemplate(
        Key key,
        string typeName,
        IReadOnlyDictionary<string, object?> values)
    {
        Key = key;
        TypeName = typeName;
        Values = values;
    }

    /// <summary>Raw value for a table entry, or null.</summary>
    public object? GetValue(string name) => Values.TryGetValue(name, out var v) ? v : null;

    /// <summary>Typed value for a table entry, or default when missing/mismatched.</summary>
    public T? GetValue<T>(string name) =>
        Values.TryGetValue(name, out var v) && v is T t ? t : default;

    public bool Has(string name) => Values.ContainsKey(name);
}
