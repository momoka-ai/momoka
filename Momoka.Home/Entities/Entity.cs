using Momoka.Home.Primitives;
using Momoka.Home.Models.Components;
using Momoka.Home.Models.States;

namespace Momoka.Home.Models.Entities;

public abstract class Entity : PropertyValueObject, IComponentSource
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public virtual Key Key => new(GetType().Name.ToLowerInvariant());

    // ── Behavior components ──────────────────────────────

    public IList<Component> Components => _components;
    private readonly List<Component> _components = new();

    public void AddComponent(Component component)
    {
        _components.Add(component);
    }

    public void RemoveComponent(Component component)
    {
        _components.Remove(component);
    }

    public T? GetComponent<T>() where T : Component =>
        _components.OfType<T>().FirstOrDefault();

    public List<T> GetComponents<T>() where T : Component =>
        _components.OfType<T>().ToList();

    public bool TryGetComponent<T>(out T result) where T : Component
    {
        var comp = GetComponent<T>();
        if (comp is not null) { result = comp; return true; }
        result = default!;
        return false;
    }

    public Component? GetComponent(Type type) =>
        _components.FirstOrDefault(type.IsInstanceOfType);

    public List<Component> GetComponents(Type type) =>
        _components.Where(type.IsInstanceOfType).ToList();

    public Component? GetComponent(Guid id) =>
        _components.FirstOrDefault(c => c.Id == id);
}
