using Momoka.Home.Entities;
using Momoka.Home.Primitives;
using Newtonsoft.Json;
namespace Momoka.Home.Serialization;

/// <summary>
/// Loads an entity config file into a typed entity. The template key is derived
/// from the file path (parent folder = namespace, filename = key path); the JSON
/// is parsed into an <see cref="EntityTemplate"/> and materialized through the
/// <see cref="EntityFactory"/>. The factory's default constructor (a plain
/// <see cref="TemplateEntity"/>) is wired in here — special entity types override
/// it by registering their own constructor for their type name.
/// </summary>
public class EntityConfigLoader
{
    private readonly EntityFactory _factory;

    /// <summary>The factory used to materialize templates; register custom type constructors on it.</summary>
    public EntityFactory Factory => _factory;

    public EntityConfigLoader(EntityFactory? factory = null)
    {
        _factory = factory ?? new EntityFactory();
        _factory.SetDefault(template => new TemplateEntity(template));
    }

    /// <summary>Loads the config file and materializes the entity.</summary>
    public Entity Load(string path) => _factory.Create(LoadTemplate(path));

    /// <summary>Parses the config file into a template (key derived from the path).</summary>
    public EntityTemplate LoadTemplate(string path)
    {
        var json = File.ReadAllText(path);
        var dto = JsonConvert.DeserializeObject<EntityConfigDto>(json)
            ?? throw new InvalidDataException($"Failed to parse entity config '{path}'.");

        var values = new Dictionary<string, object?>
        {
            ["properties"] = dto.Content?.Properties ?? new List<PropertyDto>(),
            ["components"] = dto.Content?.Components ?? new List<string>()
        };
        if (dto.Content?.Shape is { } shape)
            values["shape"] = shape;

        return new EntityTemplate(KeyFromPath(path), dto.TypeName, values);
    }

    /// <summary>Derives the template key from the file path: parent folder = namespace, filename = key path.</summary>
    public static Key KeyFromPath(string path)
    {
        var name = Path.GetFileNameWithoutExtension(path);
        var folder = Path.GetFileName(Path.GetDirectoryName(path) ?? string.Empty);
        return new Key(string.IsNullOrEmpty(folder) ? "momoka" : folder, name);
    }
}
