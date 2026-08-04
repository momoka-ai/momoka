using Momoka.Home.Components;
using Momoka.Home.Entities;
using Momoka.Home.Shapes;
using Momoka.Home.States;
namespace Momoka.Home.Serialization;

/// <summary>
/// Default concrete target for config-driven entities: a <see cref="VoxelEntity"/>
/// that adopts a typed <see cref="EntityTemplate"/> wholesale — its key as type
/// identity, plus its shape, properties and components. Special entity types
/// register their own constructor in the factory instead of using this default.
/// </summary>
public class TemplateEntity : VoxelEntity
{
    public EntityTemplate Template { get; }

    public TemplateEntity(EntityTemplate template)
    {
        Template = template;
        Key = template.Key;
        Shape = template.Shape ?? new BoxShape();

        foreach (var property in template.Properties ?? Enumerable.Empty<Property>())
            AddProperty(property);
        foreach (var component in template.Components ?? Enumerable.Empty<Component>())
            AddComponent(component);
    }
}
