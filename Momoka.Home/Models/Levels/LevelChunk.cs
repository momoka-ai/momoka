using Momoka.Home.Models.Entities;
using Momoka.Home.Primitives;

namespace Momoka.Home.Models.Levels;

/// <summary>
/// Fixed-size partition of a level. Positioned by chunk-grid coordinates.
/// XZ footprint locked at 20x20 cells, height configurable per chunk.
/// Backed by a paletted container for cache-friendly linear storage.
/// </summary>
public class LevelChunk
{
    public int SizeX { get; }
    public int SizeZ { get; }
    public int HeightY { get; }

    public Int2 Pos { get; }

    private readonly PalettedContainer<Int3, VoxelEntity> _container;

    public LevelChunk(Int2 chunkPos, int size, int heightY, int initialBits = 4)
    {
        SizeX = size;
        SizeZ = size;
        HeightY = heightY;
        Pos = chunkPos;
        _container = new(
            new Palette<VoxelEntity>.Int3ChunkStrategy(new Int3(20, heightY, 20), initialBits));
    }

    /// <summary>
    /// Preprocesses a world coordinate into chunk-local space.
    /// Returns false if the coordinate falls outside this chunk's footprint.
    /// </summary>
    private bool TryNormalize(Int3 worldPos, out Int3 localPos)
    {
        var minX = Pos.X * SizeX;
        var minZ = Pos.Z * SizeZ;

        if (worldPos.X < minX || worldPos.X >= minX + SizeX ||
            worldPos.Y < 0 || worldPos.Y >= HeightY ||
            worldPos.Z < minZ || worldPos.Z >= minZ + SizeZ)
        {
            localPos = default;
            return false;
        }

        localPos = new Int3(worldPos.X - minX, worldPos.Y, worldPos.Z - minZ);
        return true;
    }

    // public Int3 Normalize(Int3 coords)

    public bool Contains(Int3 worldPos)
    {
        var minX = Pos.X * SizeX;
        var minZ = Pos.Z * SizeZ;
        return worldPos.X >= minX && worldPos.X < minX + SizeX &&
               worldPos.Y >= 0 && worldPos.Y < HeightY &&
               worldPos.Z >= minZ && worldPos.Z < minZ + SizeZ;
    }

    public VoxelEntity? this[Int3 pos]
    {
        get => TryNormalize(pos, out var local) ? _container[local] : null;
        set { if (TryNormalize(pos, out var local)) _container[local] = value; }
    }
}
