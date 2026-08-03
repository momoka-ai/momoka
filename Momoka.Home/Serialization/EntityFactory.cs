using Momoka.Home.Entities;
namespace Momoka.Home.Serialization;

/// <summary>
/// Materializes an <see cref="EntityTemplate"/> into a typed entity instance.
/// A constructor is registered per template type name (e.g. "template_entity"
/// for plain config-driven blocks/devices, "wall" for behavior-carrying types);
/// it builds the target type and fills shape / property values / components from
/// the template's <see cref="EntityTemplate.Values"/> table. Templates stay pure
/// data — this is the only place that interprets them, so no single wrapper type
/// is forced onto every entity.
/// </summary>
public class EntityFactory
{
    private readonly Dictionary<string, Func<EntityTemplate, Entity>> _constructors = new();

    /// <summary>Registers how to build an entity for the given template type name.</summary>
    public void Register(string typeName, Func<EntityTemplate, Entity> constructor)
    {
        ArgumentNullException.ThrowIfNull(constructor);
        _constructors[typeName] = constructor;
    }

    /// <summary>
    /// Registers a simple target type with a parameterless constructor: the
    /// factory creates it and stamps the template's <see cref="Key"/> as the
    /// entity's type identity. Fill-in of shape/properties can be layered on by
    /// a custom constructor when needed.
    /// </summary>
    public void Register<TEntity>(string typeName) where TEntity : Entity, new()
    {
        Register(typeName, template => new TEntity { Key = template.Key });
    }

    /// <summary>Builds the entity for the template. Throws if no constructor is registered for its type name.</summary>
    public Entity Create(EntityTemplate template)
    {
        if (!_constructors.TryGetValue(template.TypeName, out var constructor))
        {
            throw new InvalidOperationException(
                $"No constructor registered for entity type '{template.TypeName}' (template '{template.Key}').");
        }
        return constructor(template);
    }

    /// <summary>Builds the entity for the template, or false when the type name is unregistered.</summary>
    public bool TryCreate(EntityTemplate template, out Entity? entity)
    {
        if (_constructors.TryGetValue(template.TypeName, out var constructor))
        {
            entity = constructor(template);
            return true;
        }
        entity = null;
        return false;
    }
}
