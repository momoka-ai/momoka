using Momoka.Home.Entities;
namespace Momoka.Home.Serialization;

/// <summary>
/// Default concrete target for config-driven entities: a <see cref="VoxelEntity"/>
/// that materializes itself from an <see cref="EntityTemplate"/> — adopts the
/// template's key as its type identity, builds its shape and registers its
/// properties from the template's value table. Special entity types register
/// their own constructor in the factory instead of using this default.
/// </summary>
public class TemplateEntity : VoxelEntity
{
    public EntityTemplate Template { get; }

    public TemplateEntity(EntityTemplate template)
    {
        Template = template;
        Key = template.Key;

        if (template.GetValue<ShapeDto>("shape") is { } shape)
            Shape = ShapeFactory.Create(shape);

        if (template.GetValue<List<PropertyDto>>("properties") is { } properties)
        {
            foreach (var prop in properties)
                AddProperty(PropertyFactory.Create(prop, template.Key));
        }

        // Components: keys are parsed into the value table; resolution comes later.
    }
}
