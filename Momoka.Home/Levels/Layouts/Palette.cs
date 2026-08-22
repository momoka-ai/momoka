using System.Text.Json.Serialization;
using Momoka.Home;
using Momoka.Home.Data.Json.Converters;
using Momoka.Home.Levels.Layouts;
using Momoka.Home.Primitives;
namespace Momoka.Home.Levels.Layouts;

/// <summary>
/// Bidirectional id &lt;-&gt; value mapping for palette storage.
/// Id 0 is reserved for empty (null).
/// </summary>
[JsonConverter(typeof(JsonPaletteConverterFactory))]
public class Palette<T> where T : notnull
{
    // Index 0 is the reserved "empty" slot; holds default(T) (null at runtime).
    private readonly List<T> _values = new() { default! };
    private readonly Dictionary<T, int> _index = new();

    public int Size => _values.Count;

    /// <summary>All palette values including the reserved empty slot at index 0. For storage serialization.</summary>
    internal IReadOnlyList<T> Values => _values;

    /// <summary>Rebuilds a palette from its non-empty values (ids 1..n assigned in order).</summary>
    internal static Palette<T> FromValues(IReadOnlyList<T> values)
    {
        var palette = new Palette<T>();
        foreach (var value in values)
            palette.IdFor(value);
        return palette;
    }

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
        Resized?.Invoke(PackedBitStorage.RequiredBits(_values.Count));
        return id;
    }

    public T ValueFor(int id) => _values[id];

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
        public abstract int AsIndexed(TKey key);
        public abstract TKey AsKeyed(int index);
    }

    /// <summary>
    /// Chunk-local 3D mapping: XZ locked to 20x20 cells, Y configurable.
    /// Only accepts coordinates already normalized to chunk-local space
    /// (x in [0,20), y in [0,SizeY), z in [0,20)). No chunk offset handling.
    /// </summary>
    public sealed class Int3ChunkStrategy(Int3 size, int initialBits) : Strategy<Int3>
    {
        public Int3 Size { get; } = size;
        public override int Count => Size.X * Size.Y * Size.Z;
        public override int InitialBits { get; } = initialBits;
        public override int AsIndexed(Int3 key) =>
            key.X + key.Z * Size.X + key.Y * Size.X * Size.Z;

        public override Int3 AsKeyed(int index) => new(
            index % Size.X,
            index / Size.X % Size.Z,
            index / Size.X / Size.Z
        );
    }

    /// <summary>
    /// Chunk-local 2D mapping: XZ locked to the given size, no origin.
    /// index = (x % SX) + (z % SZ) * SX
    /// </summary>
    public sealed class Int2ChunkStrategy(Int2 size, int initialBits) : Strategy<Int2>
    {
        public Int2 Size { get; } = size;
        public override int Count => Size.X * Size.Z;
        public override int InitialBits { get; } = initialBits;

        public override int AsIndexed(Int2 key) => (key.X % Size.X) + (key.Z % Size.Z) * Size.X;

        public override Int2 AsKeyed(int index) => new(index % Size.X, index / Size.X);
    }
}
