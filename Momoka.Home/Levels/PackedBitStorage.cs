using Momoka.Home;
namespace Momoka.Home.Levels;

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
