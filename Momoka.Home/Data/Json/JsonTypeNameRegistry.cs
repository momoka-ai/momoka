using System.Reflection;
namespace Momoka.Home.Data.Json;

/// <summary>
/// Attribute-driven registry mapping <see cref="JsonTypeNameAttribute"/> names to
/// concrete types, scoped by a family base type (the <c>TBase</c>
/// generic argument of each lookup method). Scoping lets the 2D and 3D
/// vocabularies share names ("polygon", "circle", "ring", "composite") without
/// colliding. Each family map is built once, lazily, by scanning the assembly.
/// </summary>
public static class JsonTypeNameRegistry
{
    private static readonly object Sync = new();
    private static readonly Dictionary<Type, Dictionary<string, Type>> ByName = new();
    private static readonly Dictionary<Type, Dictionary<Type, string>> NameByType = new();

    /// <summary>The JSON name declared on <paramref name="type"/>, or an error if none.</summary>
    public static string NameOf<TBase>(Type type) where TBase : class
    {
        var names = Ensure<TBase>().NameByType;
        return names.TryGetValue(type, out var name)
            ? name
            : throw new NotSupportedException($"Type '{type.Name}' has no [JsonTypeName].");
    }

    /// <summary>The concrete type registered under <paramref name="name"/>, or an error if unknown.</summary>
    public static Type TypeOf<TBase>(string name) where TBase : class
    {
        var types = Ensure<TBase>().ByName;
        return types.TryGetValue(name, out var type)
            ? type
            : throw new NotSupportedException($"Unknown {typeof(TBase).Name} kind '{name}'.");
    }

    /// <summary>True if <paramref name="name"/> maps to a concrete type in this family.</summary>
    public static bool TryGetType<TBase>(string name, out Type type) where TBase : class
        => Ensure<TBase>().ByName.TryGetValue(name, out type!);

    private static (Dictionary<string, Type> ByName, Dictionary<Type, string> NameByType) Ensure<TBase>() where TBase : class
    {
        lock (Sync)
        {
            if (ByName.TryGetValue(typeof(TBase), out var types))
                return (types, NameByType[typeof(TBase)]);

            var names = new Dictionary<Type, string>();
            var byName = new Dictionary<string, Type>();
            foreach (var type in typeof(TBase).Assembly.GetTypes())
            {
                if (type.IsAbstract || !typeof(TBase).IsAssignableFrom(type))
                    continue;

                var attribute = type.GetCustomAttribute<JsonTypeNameAttribute>();
                if (attribute is null)
                    continue;

                names[type] = attribute.Name;
                byName[attribute.Name] = type;
            }
            ByName[typeof(TBase)] = byName;
            NameByType[typeof(TBase)] = names;
            return (byName, names);
        }
    }
}
