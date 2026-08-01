using Momoka.Home.Primitives;

namespace Momoka.Home.Models.Levels;

/// <summary>
/// Bidirectional id &lt;-&gt; value mapping for palette storage.
/// Id 0 is reserved for empty (null).
/// </summary>
public class Palette<T> where T : notnull
{
    // Index 0 is the reserved "empty" slot; holds default(T) (null at runtime).
    private readonly List<T> _values = new() { default! };
    private readonly Dictionary<T, int> _index = new();

    public int Size => _values.Count;

    /// <summary>Raised when the palette grows — informs the container to widen its bit storage.</summary>
    public event Action<int>? Resized;

    public int IdFor(T value)
    {
        if (value is null) return 0;

        if (_index.TryGetValue(value, out var id))
            return id;

        id = _values.Count;
        _values.Add(value);
        _index[value] = id;
        Resized?.Invoke(GetRequiredBits(_values.Count));
        return id;
    }

    public T ValueFor(int id) => _values[id];

    public static int GetRequiredBits(int size) =>
        Math.Max(1, (int)Math.Ceiling(Math.Log2(size)));

    // ── Strategy ─────────────────────────────────────────

    /// <summary>
    /// Combines Minecraft's Strategy + Configuration: maps between a spatial
    /// key and a linear index, declares entry count, and selects the storage
    /// bit width appropriate for the data type.
    /// </summary>
    public abstract class Strategy<TKey> where TKey : notnull
    {
        public abstract int Count { get; }
        public abstract int InitialBits { get; }
        public abstract int ToIndex(TKey key);
        public abstract TKey FromIndex(int index);
    }

    /// <summary>
    /// Dense 3D mapping: origin plus fixed extents.
    /// index = (x-ox) + (z-oz)*SX + (y-oy)*SX*SZ
    /// </summary>
    public sealed class Int3DenseStrategy : Strategy<Int3>
    {
        public Int3 Origin { get; }
        public int SizeX { get; }
        public int SizeY { get; }
        public int SizeZ { get; }
        public override int Count => SizeX * SizeY * SizeZ;
        public override int InitialBits { get; }

        public Int3DenseStrategy(Int3 origin, int sizeX, int sizeY, int sizeZ, int initialBits = 4)
        {
            Origin = origin;
            SizeX = sizeX;
            SizeY = sizeY;
            SizeZ = sizeZ;
            InitialBits = initialBits;
        }

        public override int ToIndex(Int3 key) =>
            key.X - Origin.X + (key.Z - Origin.Z) * SizeX + (key.Y - Origin.Y) * SizeX * SizeZ;

        public override Int3 FromIndex(int index)
        {
            var x = index % SizeX;
            var z = (index / SizeX) % SizeZ;
            var y = index / (SizeX * SizeZ);
            return new Int3(Origin.X + x, Origin.Y + y, Origin.Z + z);
        }
    }

    /// <summary>
    /// Chunk-local 3D mapping: XZ locked to 20x20 cells, Y configurable.
    /// Only accepts coordinates already normalized to chunk-local space
    /// (x in [0,20), y in [0,SizeY), z in [0,20)). No chunk offset handling.
    /// </summary>
    public sealed class Int3ChunkedStrategy : Strategy<Int3>
    {
        public const int ChunkSizeX = 20;
        public const int ChunkSizeZ = 20;

        public int SizeY { get; }
        public override int Count => ChunkSizeX * SizeY * ChunkSizeZ;
        public override int InitialBits { get; }

        public Int3ChunkedStrategy(int sizeY, int initialBits = 4)
        {
            SizeY = sizeY;
            InitialBits = initialBits;
        }

        public bool Contains(Int3 localKey) =>
            localKey.X >= 0 && localKey.X < ChunkSizeX &&
            localKey.Y >= 0 && localKey.Y < SizeY &&
            localKey.Z >= 0 && localKey.Z < ChunkSizeZ;

        public override int ToIndex(Int3 localKey) =>
            localKey.X + localKey.Z * ChunkSizeX + localKey.Y * ChunkSizeX * ChunkSizeZ;

        public override Int3 FromIndex(int index)
        {
            var x = index % ChunkSizeX;
            var z = (index / ChunkSizeX) % ChunkSizeZ;
            var y = index / (ChunkSizeX * ChunkSizeZ);
            return new Int3(x, y, z);
        }
    }

    /// <summary>
    /// Dense 2D mapping for surfaces: origin plus XZ extents.
    /// index = (x-ox) + (z-oz)*SX
    /// </summary>
    public sealed class Int2DenseStrategy : Strategy<Int2>
    {
        public Int2 Origin { get; }
        public int SizeX { get; }
        public int SizeZ { get; }
        public override int Count => SizeX * SizeZ;
        public override int InitialBits { get; }

        public Int2DenseStrategy(Int2 origin, int sizeX, int sizeZ, int initialBits = 4)
        {
            Origin = origin;
            SizeX = sizeX;
            SizeZ = sizeZ;
            InitialBits = initialBits;
        }

        public override int ToIndex(Int2 key) =>
            key.X - Origin.X + (key.Z - Origin.Z) * SizeX;

        public override Int2 FromIndex(int index)
        {
            var x = index % SizeX;
            var z = index / SizeX;
            return new Int2(Origin.X + x, Origin.Z + z);
        }
    }
}
