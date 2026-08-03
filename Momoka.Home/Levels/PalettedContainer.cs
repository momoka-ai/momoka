namespace Momoka.Home;

/// <summary>
/// Paletted container: sparse entities are stored as small integer ids
/// packed into a linear bit array, with a palette mapping id &lt;-&gt; value.
/// Handles TKey &lt;-&gt; index conversion internally via a strategy and
/// automatically widens the bit storage when the palette grows.
/// </summary>
public class PalettedContainer<TKey, T> : PalettedContainerRO<TKey, T>
    where TKey : notnull where T : notnull
{
    public PalettedContainer(Palette<T>.Strategy<TKey> strategy) : base(strategy)
    {
        _palette.Resized += newBits => _storage = _storage.Resize(newBits);
    }

    public void Set(TKey key, T value)
    {
        // NOTE: resolve the palette id BEFORE touching _storage — IdFor may
        // grow the palette and resize _storage (via the Resized event). The
        // receiver of a member call is evaluated before its arguments, so
        // inlining IdFor into Set(...) would write to the OLD storage and lose
        // the very first (resize-triggering) write.
        var id = _palette.IdFor(value);
        _storage.Set(_strategy.AsIndexed(key), id);
    }

    public new T? this[TKey key]
    {
        get => _palette.ValueFor(_storage.Get(_strategy.AsIndexed(key)));
        set
        {
            if (value is null)
                Clear(key);
            else
                Set(key, value);
        }
    }

    public void Clear(TKey key) => Set(key, default!);
}
