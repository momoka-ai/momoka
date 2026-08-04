using Momoka.Home.Entities;
namespace Momoka.Home.Serialization;

/// <summary>
/// Materializes an <see cref="EntityTemplate"/> into a typed entity instance.
/// A constructor is registered per template type name (e.g. "wall" for
/// behavior-carrying types), or a single fallback constructor via
/// <see cref="SetDefault"/> (e.g. the plain <see cref="TemplateEntity"/> builder)
/// handles everything else. Templates stay pure data — this is the only place
/// that interprets them, so no single wrapper type is forced onto every entity.
/// </summary>
public class EntityFactory
{
    private readonly Dictionary<string, Func<EntityTemplate, Entity>> _constructors = new();
    private Func<EntityTemplate, Entity>? _default;

    /// <summary>Registers how to build an entity for the given template type name.</summary>
    public void Register(string typeName, Func<EntityTemplate, Entity> constructor)
    {
        ArgumentNullException.ThrowIfNull(constructor);
        _constructors[typeName] = constructor;
    }

    /// <summary>
    /// Registers the fallback constructor used when a template's type name has no
    /// explicit constructor (e.g. the default <see cref="TemplateEntity"/> builder).
    /// </summary>
    public void SetDefault(Func<EntityTemplate, Entity> constructor)
    {
        ArgumentNullException.ThrowIfNull(constructor);
        _default = constructor;
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
        var constructor = Resolve(template.Typename)
            ?? throw new InvalidOperationException(
                $"No constructor registered for entity type '{template.Typename}' (template '{template.Key}').");
        return constructor(template);
    }

    /// <summary>Builds the entity for the template, or false when no constructor is available.</summary>
    public bool TryCreate(EntityTemplate template, out Entity? entity)
    {
        var constructor = Resolve(template.Typename);
        if (constructor is null)
        {
            entity = null;
            return false;
        }
        entity = constructor(template);
        return true;
    }

    private Func<EntityTemplate, Entity>? Resolve(string typeName) =>
        _constructors.TryGetValue(typeName, out var constructor) ? constructor : _default;
}
