using Momoka.Home.Models.Entities;
using Momoka.Home.Models.Levels;
using Momoka.Home.Models.Shapes;
using Momoka.Home.Primitives;

namespace Momoka.Home.Models.Buildings;

/// <summary>
/// A physically bounded structure with floors (<see cref="Levels"/>).
/// Every enclosed habitable or usable volume — the main house, a garage,
/// a shed, a pool house — is a <see cref="Building"/>.
///
/// In external view a building is a complete <see cref="BlockEntity"/>: its
/// footprint (<see cref="Bound"/>) is exposed through <see cref="BlockEntity.Shape"/>
/// so it can be placed directly into a parent composition (e.g. the yard grid).
/// Its interior is modeled as <see cref="Levels"/>, each in building-local
/// coordinates; moving the building only updates its position in the parent grid,
/// never its interior blocks.
/// </summary>
public class Building : BlockEntity
{
    /// <summary>Inclusive 3D footprint of this building in its parent space.</summary>
    public Bound Bound { get; set; } = Bound.Empty;

    /// <summary>Floors of this building, in building-local coordinates.</summary>
    public Dictionary<int, Level> Levels { get; } = new();

    public Building()
    {
        Shape = new BoxShape { Origin = Bound.Min.ToFloat3(), SizeX = Bound.SizeX, SizeZ = Bound.SizeZ };
    }

    /// <summary>
    /// Sets the building's footprint and updates its exterior <see cref="Shape"/> accordingly.
    /// </summary>
    public void SetFootprint(Bound bound)
    {
        Bound = bound;
        Shape = new BoxShape { Origin = bound.Min.ToFloat3(), SizeX = bound.SizeX, SizeZ = bound.SizeZ };
    }
}
