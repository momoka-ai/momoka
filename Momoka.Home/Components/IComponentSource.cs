using Momoka.Home;
namespace Momoka.Home.Components;

/// <summary>
/// Capability of an object to hold behavior components — pure property carriers
/// (data sources, event sources, command targets...) attached to the host.
/// </summary>
public interface IComponentSource
{
    IList<Component> Components { get; }

    void AddComponent(Component component) => Components.Add(component);

    void RemoveComponent(Component component) => Components.Remove(component);

    T? GetComponent<T>() where T : Component => Components
        .OfType<T>()
        .FirstOrDefault();

    bool TryGetComponent<T>(out T result) where T : Component
    {
        var comp = GetComponent<T>();
        if (comp is not null) { result = comp; return true; }
        result = default!;
        return false;
    }

    Component? GetComponent(Type type) => Components
        .FirstOrDefault(type.IsInstanceOfType);

    List<Component> GetComponents(Type type) => Components
        .Where(type.IsInstanceOfType)
        .ToList();

    List<T> GetComponents<T>() where T : Component => Components
        .OfType<T>()
        .ToList();

    Component? GetComponent(Guid id) => Components
        .FirstOrDefault(c => c.Id == id);
}
