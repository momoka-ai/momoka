using Momoka.Home.Primitives;

namespace Momoka.Home;

public abstract class Shape
{
    /// <summary>
    /// Rasterizes the shape into discrete grid positions.
    /// Returns positions snapped to 10 cm grid.
    /// </summary>
    public abstract IEnumerable<Int3> GetVoxels();

    public abstract IEnumerable<Int2> GetVoxelsOnAngle();
}
