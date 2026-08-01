namespace Momoka.Home.Models.Levels;

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

    public T? Get(TKey key) =>
        _palette.ValueFor(_storage.Get(_strategy.ToIndex(key)));

    public T? this[TKey key] => Get(key);

    public int Capacity => _storage.Size;
}
