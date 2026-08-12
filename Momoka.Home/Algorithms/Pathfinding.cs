using Momoka.Home.Layouts;
using Momoka.Home.Primitives;
namespace Momoka.Home.Algorithms;

/// <summary>
/// Path-finding queries over an <see cref="IVoxelSource{T}"/> (agent-sized,
/// walkable cells). The result carries geometry only, so it is not generic.
/// </summary>
public static class Pathfinding
{
    /// <summary>An agent path (or the failure to find one): waypoints in world cm
    /// and the total travelled distance.</summary>
    public readonly record struct Result(bool Reachable, IReadOnlyList<Position> Path, double Distance);
}
