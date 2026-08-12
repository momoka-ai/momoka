using Momoka.Home.Layouts;
using Momoka.Home.Primitives;
namespace Momoka.Home.Algorithms;

/// <summary>
/// Occupancy / overlap queries over an <see cref="IVoxelSource{T}"/>.
/// </summary>
public static class Collision
{
    /// <summary>The first obstructing cell: what was hit and where.</summary>
    public readonly record struct Result<T>(bool Collided, T? Hit, Int3 Cell, Position Point)
        where T : notnull;
}
