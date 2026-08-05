using Momoka.Home.Primitives;
namespace Momoka.Home.Shapes;

/// <summary>
/// A 2D footprint in the local XZ plane (relative to the host's origin): the
/// support face of a prismatic 3D shape. Drives both placement footprints
/// (<see cref="Shape.GetVoxelsOnAngle"/>) and extrusion (<see cref="ExtrudedShape"/>).
/// </summary>
public abstract class Shape2D
{
    public abstract IEnumerable<Int2> GetCells();
}
