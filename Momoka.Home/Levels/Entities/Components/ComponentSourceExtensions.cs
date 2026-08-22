namespace Momoka.Home.Levels.Entities.Components;

/// <summary>
/// Query and mutation helpers for <see cref="IComponentSource"/> — implemented
/// once as extension methods so implementers (an entity, a residence) only need
/// to expose the component list. No per-class reimplementation.
/// </summary>
public static class ComponentSourceExtensions
{
    public static void AddComponent(this IComponentSource source, Component component) =>
        source.Components.Add(component);

    public static void RemoveComponent(this IComponentSource source, Component component) =>
        source.Components.Remove(component);

    public static T? GetComponent<T>(this IComponentSource source) where T : Component =>
        source.Components.OfType<T>().FirstOrDefault();

    public static IEnumerable<T> GetComponents<T>(this IComponentSource source) where T : Component =>
        source.Components.OfType<T>();

    public static bool TryGetComponent<T>(this IComponentSource source, out T result) where T : Component
    {
        var comp = source.GetComponent<T>();
        if (comp is not null)
        {
            result = comp;
            return true;
        }

        result = default!;
        return false;
    }

    public static Component? GetComponent(this IComponentSource source, Type type) =>
        source.Components.FirstOrDefault(type.IsInstanceOfType);

    public static List<Component> GetComponents(this IComponentSource source, Type type) =>
        source.Components.Where(type.IsInstanceOfType).ToList();

    public static Component? GetComponent(this IComponentSource source, Guid id) =>
        source.Components.FirstOrDefault(c => c.Id == id);
}
