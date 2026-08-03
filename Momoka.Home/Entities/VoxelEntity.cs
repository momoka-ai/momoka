using Momoka.Home;
using Momoka.Home.Levels;
using Momoka.Home.Primitives;
using Momoka.Home.Shapes;
namespace Momoka.Home.Entities;

public abstract class VoxelEntity : Entity
{
    /// <summary>
    /// Position of this entity relative to its parent space
    /// (a Level / Home composition). For a composition this is its
    /// offset relative to the enclosing level, so moving it moves the whole
    /// composition without touching interior coordinates.
    /// </summary>
    public Int3 Coords { get; set; } = Int3.Zero;

    public Shape Shape { get; set; } = null!;
}
