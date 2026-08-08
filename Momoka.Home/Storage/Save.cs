using Momoka.Home.Entities;
using Momoka.Home.Layouts;
using Momoka.Home.Primitives;
namespace Momoka.Home.Storage;

/// <summary>
/// A saved residence: identity (<see cref="Name"/>/<see cref="Address"/>/<see cref="Type"/>),
/// the space's <see cref="Bound"/>, the flattened entity snapshot and — once
/// loaded — the reconstructed voxel <see cref="Grid"/> and region layer. The
/// persistent form is a folder (see <see cref="SaveStore"/>): <c>Residence.json</c>,
/// <c>Entities.json</c>, <c>Regions.json</c> and <c>Chunks/Layout.{x}.{z}.dat</c>.
/// </summary>
public sealed class Save
{
    /// <summary>The save's directory (set by <see cref="SaveStore"/> on list/load).</summary>
    public string? Path { get; set; }

    public string Name { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public UnitType Type { get; set; }

    /// <summary>The space's bounds — restored from level metadata, since chunk files don't store it.</summary>
    public Bound Bound { get; set; } = Bound.Empty;

    /// <summary>The chunks subfolder name inside the save directory.</summary>
    public string ChunkLayout { get; } = "Chunks";

    /// <summary>Entities by id — the snapshot written to <c>Entities.json</c>.</summary>
    public Dictionary<Guid, Entity> Entities { get; } = new();

    /// <summary>The voxel grid, reconstructed on load from the chunk files.</summary>
    public VoxelLayout<Entity>? Grid { get; set; }

    /// <summary>The region layer, reconstructed on load from chunk columns + <c>Regions.json</c>.</summary>
    public ColumnLayout<Region>? Regions { get; set; }
}