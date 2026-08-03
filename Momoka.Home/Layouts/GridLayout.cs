using Momoka.Home;
using Momoka.Home.Levels;
using Momoka.Home.Primitives;
namespace Momoka.Home.Layouts;

/// <summary>
/// A generic chunked grid layout: items stored per grid cell, backed by a
/// paletted container per chunk for memory-efficient storage. Subclasses fix
/// the coordinate space (2D/3D) and the chunk chunking rules.
/// </summary>
public abstract class GridLayout<T, TKey>(TKey chunkSize)
    where T : notnull
    where TKey : struct
{
    public record struct Chunk(TKey Size, PalettedContainer<TKey, T> Data)
    {
        public T? this[TKey coords]
        {
            get => Data[coords];
            set => Data[coords] = value;
        }
    }

    public TKey ChunkSize { get; } = chunkSize;

    /// <summary>Non-negative remainder — C#'s % can be negative for negative operands.</summary>
    protected static int FloorMod(int value, int size) => ((value % size) + size) % size;

    private readonly Dictionary<TKey, Chunk> _innerDictionary = new();

    /// <summary>Removes all chunks, emptying the grid.</summary>
    public void Clear() => _innerDictionary.Clear();

    public abstract TKey AsChunkIndex(TKey coords);

    public abstract TKey AsChunkRelative(TKey coords);

    public abstract Palette<T>.Strategy<TKey> GetStrategy();

    public T? this[TKey coords]
    {
        get => _innerDictionary.TryGetValue(AsChunkIndex(coords), out var chunk)
            ? chunk[AsChunkRelative(coords)]
            : default;
        set
        {
            var chunkIndex = AsChunkIndex(coords);
            if (!_innerDictionary.TryGetValue(chunkIndex, out Chunk chunk))
            {
                chunk = new(ChunkSize, new(GetStrategy()));
                _innerDictionary[chunkIndex] = chunk;
            }
            chunk[AsChunkRelative(coords)] = value;
        }
    }
}

public class GridLayout3D<T> : GridLayout<T, Int3>
    where T : notnull
{
    public GridLayout3D(Int3 chunkSize) : base(chunkSize)
    {
    }

    public override Int3 AsChunkIndex(Int3 coords) => new(
        (coords.X >= 0 ? coords.X : coords.X - (ChunkSize.X - 1)) / ChunkSize.X,
        (coords.Y >= 0 ? coords.Y : coords.Y - (ChunkSize.Y - 1)) / ChunkSize.Y,
        (coords.Z >= 0 ? coords.Z : coords.Z - (ChunkSize.Z - 1)) / ChunkSize.Z
    );

    public override Int3 AsChunkRelative(Int3 coords) => new(
        FloorMod(coords.X, ChunkSize.X),
        FloorMod(coords.Y, ChunkSize.Y),
        FloorMod(coords.Z, ChunkSize.Z)
    );

    public override Palette<T>.Strategy<Int3> GetStrategy() => new Palette<T>.Int3ChunkStrategy(ChunkSize, 4);
}

public class GridLayout2D<T> : GridLayout<T, Int2>
    where T : notnull
{
    public GridLayout2D(Int2 chunkSize) : base(chunkSize)
    {
    }

    public override Int2 AsChunkIndex(Int2 coords) => new(
        (coords.X >= 0 ? coords.X : coords.X - (ChunkSize.X - 1)) / ChunkSize.X,
        (coords.Z >= 0 ? coords.Z : coords.Z - (ChunkSize.Z - 1)) / ChunkSize.Z
    );

    public override Int2 AsChunkRelative(Int2 coords) => new(
        FloorMod(coords.X, ChunkSize.X),
        FloorMod(coords.Z, ChunkSize.Z)
    );

    public override Palette<T>.Strategy<Int2> GetStrategy() => new Palette<T>.Int2ChunkStrategy(ChunkSize, 4);
}