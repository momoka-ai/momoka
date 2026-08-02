namespace Momoka.Home.Models.Levels;

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
        _storage.Set(_strategy.AsIndexed(key), _palette.IdFor(value));
    }

    public new T? this[TKey key]
    {
        get => _palette.ValueFor(_storage.Get(_strategy.AsIndexed(key)));
        set
        {
            if (value is null)
                Clear(key);
            else
                _storage.Set(_strategy.AsIndexed(key), _palette.IdFor(value));
        }
    }

    public void Clear(TKey key) => Set(key, default!);
}
