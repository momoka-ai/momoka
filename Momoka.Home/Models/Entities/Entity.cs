using Momoka.Home.Primitives;
using Momoka.Home.Models.States;

namespace Momoka.Home.Models.Entities;

public abstract class Entity : PropertyValueObject
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public virtual Key Key => new(GetType().Name.ToLowerInvariant());

    // ── Spatial hierarchy ────────────────────────────────

    public Entity? Parent { get; private set; }
    public IReadOnlyList<Entity> Children => _children;
    protected readonly List<Entity> _children = new();

    public void AddChild(Entity child)
    {
        child.Parent?._children.Remove(child);
        child.Parent = this;
        _children.Add(child);
    }

    public void RemoveChild(Entity child)
    {
        if (child.Parent == this)
        {
            _children.Remove(child);
            child.Parent = null;
        }
    }

    public T? GetChild<T>() where T : Entity =>
        _children.OfType<T>().FirstOrDefault();

    public List<T> GetChildren<T>() where T : Entity =>
        _children.OfType<T>().ToList();

    public Entity? FindChild(Guid id)
    {
        foreach (var child in _children)
        {
            if (child.Id == id) return child;
            var found = child.FindChild(id);
            if (found is not null) return found;
        }
        return null;
    }

    public Entity? GetChild(Guid id) =>
        _children.FirstOrDefault(c => c.Id == id);

    public IEnumerable<Entity> Traverse()
    {
        yield return this;
        foreach (var child in _children)
            foreach (var sub in child.Traverse())
                yield return sub;
    }

    // ── Behavior components ──────────────────────────────

    public IReadOnlyList<Entity> Components => _components;
    private readonly List<Entity> _components = new();

    public void AddComponent(Entity component)
    {
        _components.Add(component);
    }

    public void RemoveComponent(Entity component)
    {
        _components.Remove(component);
    }

    public T? GetComponent<T>() where T : Entity =>
        _components.OfType<T>().FirstOrDefault();

    public List<T> GetComponents<T>() where T : Entity =>
        _components.OfType<T>().ToList();

    public T? GetComponentInChildren<T>() where T : Entity
    {
        var match = GetComponent<T>();
        if (match is not null) return match;
        foreach (var child in _children)
        {
            match = child.GetComponentInChildren<T>();
            if (match is not null) return match;
        }
        return null;
    }

    public bool TryGetComponent<T>(out T result) where T : Entity
    {
        var comp = GetComponent<T>();
        if (comp is not null) { result = comp; return true; }
        result = default!;
        return false;
    }

    public Entity? GetComponent(Type type) =>
        _components.FirstOrDefault(type.IsInstanceOfType);

    public List<Entity> GetComponents(Type type) =>
        _components.Where(type.IsInstanceOfType).ToList();

    public Entity? GetComponentInChildren(Type type)
    {
        var match = GetComponent(type);
        if (match is not null) return match;
        foreach (var child in _children)
        {
            match = child.GetComponentInChildren(type);
            if (match is not null) return match;
        }
        return null;
    }

    public Entity? GetComponent(Guid id) =>
        _components.FirstOrDefault(c => c.Id == id);

    public Entity? GetComponentInChildren(Guid id)
    {
        var match = GetComponent(id);
        if (match is not null) return match;
        foreach (var child in _children)
        {
            match = child.GetComponentInChildren(id);
            if (match is not null) return match;
        }
        return null;
    }
}
