using Momoka.Home.Models.Entities;

namespace Momoka.Home.Models.Interfaces;

/// <summary>
/// Capability of a space to carry a wall/fence boundary topology: a 2D graph
/// whose edges are linear boundary segments (walls, fences). Used for
/// exterior-ring, room, and opening analysis.
/// </summary>
public interface IWallGraph
{
    Graph2D<BlockEntity> WallGraph { get; }
}
