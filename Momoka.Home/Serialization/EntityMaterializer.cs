using Momoka.Home.Entities;
using Momoka.Home.Primitives;
using Momoka.Home.Shapes;
using Momoka.Home.States;
namespace Momoka.Home.Serialization;

/// <summary>
/// Default materializer for config-driven entities: builds an <see cref="Entity{T}"/>
/// — currently <c>Entity&lt;Int3&gt;</c> — from a template: stamps the key, fills the
/// shape and clones the properties per instance (so entities never share property
/// values). Special entity types override it by registering their own constructor
/// for their type name.
/// </summary>
public static class EntityMaterializer
{
    public static Entity<Int3> Build(EntityTemplate template)
    {
        var entity = new Entity<Int3>
        {
            Key = template.Key,
            Shape = template.Shape ?? new BoxShape()
        };
        entity.AddProperties(template.Properties?.Select(p => p.Clone()) ?? Enumerable.Empty<Property>());
        // Components: template component keys parsed; resolution comes later.
        return entity;
    }
}
