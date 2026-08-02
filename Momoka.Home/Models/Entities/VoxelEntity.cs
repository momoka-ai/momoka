using Momoka.Home.Models.Shapes;
using Momoka.Home.Primitives;

namespace Momoka.Home.Models.Entities;

public abstract class VoxelEntity : Entity
{
    /// <summary>
    /// Position of this entity relative to its parent space
    /// (a <see cref="VoxelGridEntity"/>). For a composition this is its
    /// offset relative to the enclosing level, so moving it moves the whole
    /// composition without touching interior coordinates.
    /// </summary>
    public Int3 Coords { get; set; } = Int3.Zero;

    public Shape Shape { get; set; } = null!;
}
