using Momoka.Home.Layouts;
using Momoka.Home.Primitives;
namespace Momoka.Home.Algorithms;

/// <summary>
/// Ray-casting queries over an <see cref="IVoxelSource{T}"/>. Tracing returns
/// <see cref="Result{T}"/>? — null when the ray leaves the grid without a match.
/// </summary>
public static class RayTracing
{
    /// <summary>A single ray hit: the first matching cell, its exact intersection
    /// point (world cm) and the distance from the origin.</summary>
    public readonly record struct Result<T>(T? Value, Int3 Cell, Position Point, double Distance)
        where T : notnull;
}
