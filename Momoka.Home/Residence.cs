using Momoka.Home.Components;
using Momoka.Home.Entities;
using Momoka.Home.Layouts;
using Momoka.Home.Primitives;
namespace Momoka.Home;

/// <summary>
/// The total container of a home — one family's residence. Identity
/// (<see cref="Name"/>/<see cref="Address"/>), the kind of residence
/// (<see cref="Type"/>), the single flattened 3D space (<see cref="Layout"/>),
/// and whole-home behavior components.
/// </summary>
public class Residence : IEntitySource, IComponentSource
{
    public string Name { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public UnitType Type { get; set; }
    public UnitLayout Layout { get; set; } = new();

    /// <summary>All entities of the residence — the unit layout's root space.</summary>
    public IReadOnlyList<Entity> Entities => Layout.Entities;

    /// <summary>Every placement surface of the residence (floors, walls, shelves…).</summary>
    public IEnumerable<GridLayout<bool>> Surfaces => Layout.Surfaces;

    public IList<Component> Components => _components;
    private readonly List<Component> _components = new();
}