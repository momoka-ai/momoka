using Momoka.Home;
namespace Momoka.Home.Levels.Layouts;

/// <summary>
/// Read-only view of a paletted container. Handles TKey &lt;-&gt; index conversion
/// via an injected strategy. Exposes value lookup and capacity, no mutation.
/// </summary>
public class PalettedContainerRO<TKey, T> where TKey : notnull where T : notnull
{
    protected readonly Palette<T> _palette;
    protected readonly Palette<T>.Strategy<TKey> _strategy;
    protected PackedBitStorage _storage;

    protected PalettedContainerRO(Palette<T>.Strategy<TKey> strategy)
    {
        _strategy = strategy;
        _palette = new Palette<T>();
        _storage = new PackedBitStorage(strategy.Count, strategy.InitialBits);
    }

    /// <summary>Restores a container from serialized palette + storage.</summary>
    protected PalettedContainerRO(Palette<T>.Strategy<TKey> strategy, Palette<T> palette, PackedBitStorage storage)
    {
        _strategy = strategy;
        _palette = palette;
        _storage = storage;
    }

    /// <summary>Palette, for storage serialization.</summary>
    internal Palette<T> Palette => _palette;

    /// <summary>Packed bit storage, for storage serialization.</summary>
    internal PackedBitStorage Storage => _storage;

    public T? Get(TKey key) =>
        _palette.ValueFor(_storage.Get(_strategy.AsIndexed(key)));

    public T? this[TKey key] => Get(key);

    public int Capacity => _storage.Size;
}
