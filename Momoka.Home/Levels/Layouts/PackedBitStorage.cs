using Momoka.Home;
using System.Runtime.InteropServices;
namespace Momoka.Home.Levels.Layouts;

/// <summary>
/// Fixed-width packed bit array. Stores integer ids in an ulong[],
/// packing multiple values per 64-bit word. Cache-friendly linear storage.
/// </summary>
public sealed class PackedBitStorage
{
    private readonly ulong[] _data;
    private readonly int _size;
    private readonly int _bits;
    private readonly ulong _mask;
    private readonly int _valuesPerLong;

    public int Size => _size;
    public int Bits => _bits;

    /// <summary>Raw packed words, for storage serialization.</summary>
    internal ulong[] Data => _data;

    /// <summary>
    /// The raw words as little-endian bytes, for BLOB storage (Sqlite
    /// <c>data</c> column). Byte layout matches <c>BinaryWriter.Write(ulong)</c>
    /// as used by the chunk codec.
    /// </summary>
    internal byte[] ToBytes() => MemoryMarshal.AsBytes(_data.AsSpan()).ToArray();

    /// <summary>
    /// Restores storage from little-endian bytes produced by <see cref="ToBytes"/>.
    /// </summary>
    internal static PackedBitStorage FromBytes(int size, int bits, byte[] data)
    {
        if (data.Length % sizeof(ulong) != 0)
            throw new ArgumentException($"Raw BLOB length {data.Length} is not a multiple of 8.", nameof(data));

        var words = new ulong[data.Length / sizeof(ulong)];
        data.CopyTo(MemoryMarshal.AsBytes(words.AsSpan()));
        return new PackedBitStorage(size, bits, words);
    }

    /// <summary>Restores storage from raw packed words (length must match size/bits).</summary>
    internal PackedBitStorage(int size, int bits, ulong[] data) : this(size, bits)
    {
        if (data.Length != _data.Length)
            throw new ArgumentException($"Raw storage length {data.Length} != expected {_data.Length} for {size} cells × {bits} bits.", nameof(data));
        _data = data;
    }

    /// <summary>True when every cell is empty (id 0) — the storage holds no data.</summary>
    internal bool AllZero()
    {
        foreach (var word in _data)
            if (word != 0)
                return false;
        return true;
    }

    /// <summary>Minimum number of bits needed to store <paramref name="size"/> distinct values.</summary>
    public static int RequiredBits(int size) =>
        Math.Max(1, (int)Math.Ceiling(Math.Log2(size)));

    public PackedBitStorage(int size, int bits)
    {
        if (bits < 1 || bits > 63)
            throw new ArgumentOutOfRangeException(nameof(bits), "Bits must be in [1, 63].");

        _size = size;
        _bits = bits;
        _mask = (1UL << bits) - 1;
        _valuesPerLong = 64 / bits;
        _data = new ulong[(size + _valuesPerLong - 1) / _valuesPerLong];
    }

    public int Get(int index)
    {
        var startBit = index * _bits;
        var longIndex = startBit >> 6;
        var offset = startBit & 63;

        var value = _data[longIndex] >> offset;
        if (offset + _bits > 64 && longIndex + 1 < _data.Length)
            value |= _data[longIndex + 1] << (64 - offset);

        return (int)(value & _mask);
    }

    public void Set(int index, int value)
    {
        var startBit = index * _bits;
        var longIndex = startBit >> 6;
        var offset = startBit & 63;

        var mask = _mask << offset;
        _data[longIndex] = (_data[longIndex] & ~mask) | ((ulong)value << offset);

        if (offset + _bits > 64 && longIndex + 1 < _data.Length)
        {
            var rem = 64 - offset;
            var tailMask = _mask >> rem;
            _data[longIndex + 1] = (_data[longIndex + 1] & ~tailMask) | ((ulong)value >> rem);
        }
    }

    /// <summary>Creates a new storage with a different bit width, copying all values.</summary>
    public PackedBitStorage Resize(int newBits)
    {
        var resized = new PackedBitStorage(_size, newBits);
        for (var i = 0; i < _size; i++)
            resized.Set(i, Get(i));
        return resized;
    }
}
