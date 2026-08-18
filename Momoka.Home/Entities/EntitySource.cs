using Momoka.Home.Primitives;

namespace Momoka.Home.Entities;

/// <summary>
/// A spatial container that exposes the entities it holds — implemented by
/// <see cref="Momoka.Home.UnitLayout"/> (placed entities) and
/// <see cref="Momoka.Home.Residence"/> (the full catalog). This is the uniform
/// way to enumerate their contents for queries, save/load and agentic perception.
/// </summary>
public interface IEntitySource
{
    List<Entity> Entities { get; }
}

/// <summary>
/// Origin-based entity queries on <see cref="IEntitySource"/> — every helper
/// considers only each entity's <see cref="Entity.Pos"/> (its origin), never
/// its <see cref="Entity.Volume"/> shape.
/// </summary>
public static class EntitySourceExtensions
{
    public static IEnumerable<Entity> FindEntitiesAtPoint(this IEntitySource source, Position position) =>
        source.Entities.Where(e => e.Pos == position);

    public static IEnumerable<Entity> FindNearbyEntities(this IEntitySource source, Position position, float radius) =>
        source.Entities.Where(e => (e.Pos - position)
            .AsFloat3()
            .Magnitude <= radius);

    public static IEnumerable<Entity> FindNearbyEntities(this IEntitySource source, Position position, float x, float y, float z) =>
        source.Entities.Where(e =>
        {
            var d = (e.Pos - position).AsFloat3();
            return Math.Abs(d.X) <= x && Math.Abs(d.Y) <= y && Math.Abs(d.Z) <= z;
        });

    public static IEnumerable<Entity> FindNearbyEntities(this IEntitySource source, Position position, Float3 size) =>
        source.Entities.Where(e =>
        {
            var d = (e.Pos - position).AsFloat3();
            return Math.Abs(d.X) <= size.X && Math.Abs(d.Y) <= size.Y && Math.Abs(d.Z) <= size.Z;
        });

    public static IEnumerable<Entity> FindEntitiesInBound(this IEntitySource source, Bound bound) =>
        source.Entities.Where(e => bound.Contains(e.Pos.Absolute()));

    public static IEnumerable<Entity> FindEntitiesInBound(this IEntitySource source, Float3 min, Float3 max) =>
        source.FindEntitiesInBound(Bound.FromCorners(min, max));

    public static IEnumerable<Entity> FindEntitiesOfKeyed(this IEntitySource source, Key key) =>
        source.Entities.Where(e => e.Key == key);
}
