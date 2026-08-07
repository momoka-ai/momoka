using Momoka.Home.Entities;
using Momoka.Home.Layouts;
using Momoka.Home.Primitives;
namespace Momoka.Home;

/// <summary>
/// The polymorphic spatial root of a home — the thing that IS the home's living
/// space: a single <see cref="Level"/> (apartment), a stacked multi-level space
/// (loft/duplex), or an estate (a house with buildings). Exposes the aggregate
/// voxel container and the placement surfaces of the whole space, so a
/// <see cref="Home"/> can treat every home layout uniformly.
/// </summary>
public interface IVoxelSpaceRoot : IEntitySource
{
    /// <summary>Aggregate 3D occupancy container covering the whole space.</summary>
    VoxelLayout<Entity<Int3>> Layout { get; }

    /// <summary>Every placement surface of the whole space (floors, walls, shelves…).</summary>
    IEnumerable<VoxelLayout2D> Surfaces { get; }
}
