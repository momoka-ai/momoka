using Momoka.Home.Primitives;

namespace Momoka.Home.Models.Layouts;

public class Canvas<T, TPos> where TPos : notnull
{
    public Dictionary<TPos, T> Entities { get; } = new();

    public void Place(T entity, TPos pos)
    {
        Entities[pos] = entity;
    }

    public T? GetEntity(TPos pos) =>
        Entities.TryGetValue(pos, out var entity) ? entity : default;

    public bool Remove(TPos pos) => Entities.Remove(pos);

    public bool HasEntity(TPos pos) => Entities.ContainsKey(pos);
}
