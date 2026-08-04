using Momoka.Home.Primitives;
namespace Momoka.Home.Serialization;

/// <summary>
/// Registry of entity templates: pre-registered base types (e.g. "voxelentity")
/// plus every template loaded from config. Used to resolve a config's "class"
/// to its inherited template, and to make loaded templates inheritable in turn.
/// </summary>
public class EntityTemplateRegistry
{
    private readonly Dictionary<string, EntityTemplate> _byName = new();

    /// <summary>Registers a loaded template under its key (e.g. "midea:air_conditioner.ac_1523").</summary>
    public void Register(EntityTemplate template) => _byName[template.Key.ToString()] = template;

    /// <summary>Registers a template under an explicit name (e.g. a base type "voxelentity").</summary>
    public void Register(string name, EntityTemplate template) => _byName[name] = template;

    /// <summary>Finds the template a config's "typename" refers to, or null.</summary>
    public EntityTemplate? Resolve(string name) =>
        _byName.TryGetValue(name, out var template) ? template : null;

    public bool TryResolve(string name, out EntityTemplate? template) =>
        _byName.TryGetValue(name, out template);

    public IEnumerable<EntityTemplate> All => _byName.Values;
}
