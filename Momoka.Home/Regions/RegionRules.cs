using Momoka.Home.Entities;
namespace Momoka.Home.Regions;

/// <summary>
/// Decides which entities block region labeling. Blocking = structural partitions
/// (walls, floors, ceilings, doors, windows, … — matched by Key path) plus any
/// object at least <see cref="TallThreshold"/> cells tall (1.8 m / 10 cm).
/// Everything else is transparent to the flood fill — furniture lets the fill
/// flow around it (its own cells are simply not part of any region). The
/// <see cref="MaxStep"/> tolerance connects adjacent columns' spans within a
/// human step height; finer height-difference handling belongs to pathfinding.
/// </summary>
public sealed class RegionRules
{
    private static readonly HashSet<string> DefaultStructural = new()
    {
        "wall", "floor", "ceiling", "door", "window",
        "stair", "staircase", "column", "beam", "roof", "partition", "fence",
    };

    private readonly HashSet<string>? _structural;

    /// <summary>Connectivity tolerance between adjacent spans in cells; 2 ≈ 20 cm human step.</summary>
    public int MaxStep { get; init; } = 2;

    /// <summary>Objects this tall (cells) block even when not structural; 18 ≈ 1.8 m.</summary>
    public int TallThreshold { get; init; } = 18;

    public RegionRules(IEnumerable<string>? structuralPaths = null)
    {
        _structural = structuralPaths is null ? null : new HashSet<string>(structuralPaths);
    }

    /// <summary>True if the entity partitions space (walls, floors, ceilings, doors, …).</summary>
    public bool IsStructural(Entity entity)
    {
        var path = entity.Key.Path;
        var dot = path.IndexOf('.');
        var head = dot > 0 ? path[..dot] : path;
        return _structural is null ? DefaultStructural.Contains(head) : _structural.Contains(head);
    }

    /// <summary>True if the entity blocks region labeling.</summary>
    public bool IsBlocking(Entity entity) =>
        entity.Volume is not null && (IsStructural(entity) || HeightOf(entity) >= TallThreshold);

    private static int HeightOf(Entity entity)
    {
        var minY = int.MaxValue;
        var maxY = int.MinValue;
        foreach (var cell in entity.Volume!.Cells3D())
        {
            if (cell.Y < minY) minY = cell.Y;
            if (cell.Y > maxY) maxY = cell.Y;
        }
        return minY > maxY ? 0 : maxY - minY + 1;
    }
}
