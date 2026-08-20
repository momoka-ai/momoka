using Momoka.Home.Geometry;
using Momoka.Home.Primitives;
using Momoka.Home.Entities;
using Momoka.Home.Data.Json;
using Momoka.Home.Data.Json.Converters;
using Newtonsoft.Json;
namespace Momoka.Home.Entities;

/// <summary>
/// The single entry point of the config-driven entity pipeline: loads entity
/// config files into <see cref="EntityTemplate"/>s (key derived from the path,
/// "extends" resolved against the registry as mixin composition), holds the
/// template registry, and materializes templates into entities
/// (<see cref="Entity"/>).
/// </summary>
public class EntityTemplateFactory
{
    private static readonly JsonSerializerSettings Settings = new()
    {
        Converters = { new JsonGeometryConverter(), new JsonPropertyConverter() }
    };

    private readonly Dictionary<string, EntityTemplate> _templates = new();

    /// <summary>All registered templates.</summary>
    public IEnumerable<EntityTemplate> All => _templates.Values;

    /// <summary>
    /// 模板目录版本（服务器装载时设置；客户端快照携带，create_entity 请求带
    /// templateVersion 供"目录过期"校验）。热更新只影响新实例化。
    /// </summary>
    public string Version { get; set; } = "1";

    // ── Registry ─────────────────────────────────────────

    /// <summary>Registers a template under a name (mixins and base facets).</summary>
    public void Register(string name, EntityTemplate template) => _templates[name] = template;

    /// <summary>Finds a template by its registered name (an "extends" reference), or null.</summary>
    public EntityTemplate? Resolve(string name) =>
        _templates.TryGetValue(name, out var template) ? template : null;

    // ── Config files ─────────────────────────────────────

    /// <summary>Loads the config file into a template (key from path, "extends" composed).</summary>
    public EntityTemplate LoadTemplate(string path)
    {
        var key = KeyFromPath(path);
        var template = JsonConvert.DeserializeObject<EntityTemplate>(File.ReadAllText(path), Settings)
            ?? throw new InvalidDataException($"Failed to parse entity config '{path}'.");
        template.Key = key;
        return Compose(template);
    }

    /// <summary>Loads the config file and materializes the entity.</summary>
    public Entity Load(string path) => Create(LoadTemplate(path));

    /// <summary>Writes a template back to a config file (round-trip).</summary>
    public void Save(string path, EntityTemplate template)
    {
        var json = JsonConvert.SerializeObject(template, Formatting.Indented, Settings);
        File.WriteAllText(path, json);
    }

    /// <summary>Derives the template key from the file path: parent folder = namespace, filename = key path.</summary>
    public static Key KeyFromPath(string path)
    {
        var name = Path.GetFileNameWithoutExtension(path);
        var folder = Path.GetFileName(Path.GetDirectoryName(path) ?? string.Empty);
        return new Key(string.IsNullOrEmpty(folder) ? "momoka" : folder, name);
    }

    // ── Mixin composition ────────────────────────────────

    /// <summary>
    /// Resolves "extends" against the registry and merges each mixin in array
    /// order — later entries override earlier ones by name; this config's own
    /// fields override everything. Registers the composed template under its key.
    /// </summary>
    private EntityTemplate Compose(EntityTemplate template)
    {
        var merged = new EntityTemplate { Key = template.Key };
        foreach (var baseName in template.Extends)
        {
            var mixin = Resolve(baseName)
                ?? throw new InvalidDataException($"Template '{template.Key}' extends unknown template '{baseName}'.");
            MergeInto(merged, mixin);
        }
        MergeInto(merged, template);
        Register(merged.Key.ToString(), merged);
        return merged;
    }

    private static void MergeInto(EntityTemplate target, EntityTemplate source)
    {
        if (source.Volume is { } volume)
            target.Volume = volume;

        var targetProps = target.Properties ??= new List<Property>();
        foreach (var property in source.Properties ?? Enumerable.Empty<Property>())
        {
            var index = targetProps.FindIndex(p => p.Name == property.Name);
            if (index >= 0) targetProps[index] = property;
            else targetProps.Add(property);
        }

        var targetComponents = target.Components ??= new List<string>();
        foreach (var component in source.Components ?? Enumerable.Empty<string>())
        {
            if (!targetComponents.Contains(component))
                targetComponents.Add(component);
        }
    }

    // ── Materialize ──────────────────────────────────────

    /// <summary>
    /// Builds an <see cref="Entity"/> from the template: stamps
    /// the key, fills the shape and clones the properties per instance (so
    /// entities never share property values).
    /// </summary>
    public Entity Create(EntityTemplate template)
    {
        var entity = new Entity
        {
            Key = template.Key,
            Volume = template.Volume ?? new Box3D()
        };
        entity.AddProperties(template.Properties?.Select(p => p.Clone()) ?? Enumerable.Empty<Property>());
        return entity;
    }
}
