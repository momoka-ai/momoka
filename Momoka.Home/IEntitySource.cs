using Momoka.Home.Entities;
namespace Momoka.Home;

/// <summary>
/// A spatial container that exposes the entities it holds (a Home, a Level).
/// Containers are hand-built, not config-instantiated — this is the uniform way
/// to enumerate their contents for queries, save/load and agentic perception.
/// </summary>
public interface IEntitySource
{
    IReadOnlyList<Entity> Entities { get; }
}
