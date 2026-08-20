using Momoka.Home.Entities;
using Momoka.Home.Layouts;
using Momoka.Home.Primitives;
using Newtonsoft.Json;
namespace Momoka.Home;

/// <summary>
/// The total container of a home — one family's residence. Identity
/// (<see cref="Name"/>/<see cref="Address"/>), the kind of residence
/// (<see cref="Type"/>), the single flattened 3D space (<see cref="Layout"/>),
/// and whole-home behavior components. The residence (minus
/// <see cref="Entities"/>) serializes directly via
/// <see cref="Settings.JsonSerialization"/> — the entity registry is
/// <c>[JsonIgnore]</c> because it lives as rows of the <c>Entities</c> table
/// (see <see cref="Momoka.Home.Data.Sqlite.SqliteStore"/>).
/// </summary>
public class Residence : IEntitySource, IComponentSource
{
    [JsonProperty("name")]
    public string Name { get; set; } = string.Empty;

    [JsonProperty("address")]
    public string Address { get; set; } = string.Empty;

    [JsonProperty("type")]
    public UnitType Type { get; set; }

    [JsonIgnore]
    public UnitLayout Layout { get; set; } = new();

    /// <summary>
    /// The space's bounds — persisted metadata. Kept in sync with the grid by
    /// the persistence layer.
    /// </summary>
    [JsonProperty("bound")]
    public Bound Bound { get; set; } = Bound.UnsetValue;

    [JsonProperty("components")]
    public List<Component> Components { get; set; } = new();

    /// <summary>
    /// All known (registered) entities of the residence — including objects not
    /// yet placed in <see cref="Layout"/> (e.g. paired appliances awaiting a
    /// spot). Registration happens at creation; placement is
    /// <see cref="UnitLayout.Add(Entity)"/>'s job, which writes the entity into the
    /// grid and registers it in <see cref="UnitLayout.Entities"/> (the placed
    /// subset). Not serialized in the residence JSON: each entity is a row of
    /// the <c>Entities</c> table.
    /// </summary>
    [JsonIgnore]
    public List<Entity> Entities { get; set; } = new();
}