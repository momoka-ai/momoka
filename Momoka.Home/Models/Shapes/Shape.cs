using Momoka.Home.Primitives;

namespace Momoka.Home.Models.Shapes;

public abstract class Shape
{
    /// <summary>
    /// Rasterizes the shape into discrete grid positions.
    /// Returns positions snapped to 10 cm grid.
    /// </summary>
    public abstract IEnumerable<Float3> Locations();
}
