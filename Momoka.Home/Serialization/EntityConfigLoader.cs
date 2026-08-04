using Momoka.Home.Entities;
using Momoka.Home.Primitives;
using Momoka.Home.States;
using Newtonsoft.Json;
namespace Momoka.Home.Serialization;

/// <summary>
/// Loads entity config files into typed <see cref="EntityTemplate"/>s and
/// materializes entities:
///  1. the key is derived from the file path (folder = namespace, filename = key path);
///  2. the config's "class" is resolved against the template registry to find the
///     inherited template, which is copied as the base;
///  3. the config's remaining content (shape, properties) is merged into the copy,
///     which is then stored in the registry.
/// The factory's default constructor (a plain <see cref="TemplateEntity"/>) is
/// wired in here — special entity types override it by registering their own
/// constructor for their type name.
/// </summary>
public class EntityConfigLoader
{
    private readonly EntityFactory _factory;
    private readonly EntityTemplateRegistry _registry;

    /// <summary>The factory used to materialize templates; register custom type constructors on it.</summary>
    public EntityFactory Factory => _factory;

    /// <summary>The template registry: pre-registered base types plus every loaded template.</summary>
    public EntityTemplateRegistry Registry => _registry;

    public EntityConfigLoader(EntityFactory? factory = null)
    {
        _factory = factory ?? new EntityFactory();
        _factory.SetDefault(template => new TemplateEntity(template));
        _registry = new EntityTemplateRegistry();
        _registry.Register("voxelentity", new EntityTemplate
        {
            Key = new Key("voxelentity"),
            Class = "voxelentity",
            Type = typeof(VoxelEntity)
        });
    }

    /// <summary>Loads the config file and materializes the entity.</summary>
    public Entity Load(string path) => _factory.Create(LoadTemplate(path));

    /// <summary>Loads the config file into a template (key derived from the path).</summary>
    public EntityTemplate LoadTemplate(string path)
    {
        var key = KeyFromPath(path);
        var dto = JsonConvert.DeserializeObject<EntityConfigDto>(File.ReadAllText(path))
            ?? throw new InvalidDataException($"Failed to parse entity config '{path}'.");
        return Build(key, dto);
    }

    /// <summary>
    /// Builds a template from a DTO: inherits the parent template (resolved by
    /// "class"), then merges this config's shape and properties over it.
    /// </summary>
    public EntityTemplate Build(Key key, EntityConfigDto dto)
    {
        var parent = _registry.Resolve(dto.Class);
        var template = new EntityTemplate
        {
            Key = key,
            Class = dto.Class,
            Type = parent?.Type,
            Shape = parent?.Shape,
            Properties = parent?.Properties?.ToList(),
            Components = parent?.Components?.ToList()
        };

        if (dto.Shape is { } shape)
            template.Shape = ShapeFactory.Create(shape);

        var childProperties = dto.Properties?.Select(p => PropertyFactory.Create(p, key));
        template.Properties = MergeByKey(parent?.Properties, childProperties);

        // Components: child keys parsed; resolution comes later.
        _registry.Register(template);
        return template;
    }

    /// <summary>Merges child properties over parent's, replacing by name.</summary>
    private static List<Property> MergeByKey(IEnumerable<Property>? parent, IEnumerable<Property>? child)
    {
        var result = parent?.ToList() ?? new List<Property>();
        foreach (var property in child ?? Enumerable.Empty<Property>())
        {
            var index = result.FindIndex(p => p.Name == property.Name);
            if (index >= 0) result[index] = property;
            else result.Add(property);
        }
        return result;
    }

    /// <summary>Derives the template key from the file path: parent folder = namespace, filename = key path.</summary>
    public static Key KeyFromPath(string path)
    {
        var name = Path.GetFileNameWithoutExtension(path);
        var folder = Path.GetFileName(Path.GetDirectoryName(path) ?? string.Empty);
        return new Key(string.IsNullOrEmpty(folder) ? "momoka" : folder, name);
    }
}
