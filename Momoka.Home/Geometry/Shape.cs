using Momoka.Home;
using Momoka.Home.Primitives;
namespace Momoka.Home.Geometry;

/// <summary>
/// A 2D footprint in the local XZ plane (relative to the host's origin): the
/// support face of a prismatic <see cref="Volume"/>. Drives placement footprints
/// and extrusion (<see cref="Extruded3D"/>). The 2D counterpart of
/// <see cref="Volume"/> — a shape is a 2D pattern, a volume is its 3D occupation.
/// </summary>
public abstract class Shape : IVoxelGeometry2D
{
    public abstract IEnumerable<Int2> Cells2D();
}
