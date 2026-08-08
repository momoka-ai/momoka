using Momoka.Home.Primitives;
namespace Momoka.Home.Layouts;

/// <summary>
/// A pure chunked 3D voxel grid holding arbitrary values
/// (<typeparamref name="T"/>) with no entity or placement semantics — the
/// successor of <c>VoxelLayout3D</c>. Storage is Minecraft-style: XZ chunks
/// (<see cref="VoxelChunk{T}"/>) keyed by a packed long, each a column of
/// <see cref="VoxelChunkSection{T}"/> (16×16×16 paletted sections) along the
/// height axis. Sections are created lazily, so empty bands cost nothing and
/// adding height is just appending a section — the chunk structure never needs
/// recomputing. Entity placement and the entity list live on
/// <see cref="Momoka.Home.UnitLayout"/>.
/// </summary>
public class VoxelLayout<T> where T : notnull
{
    /// <summary>Section edge length — a power of two, so chunk math is shift/mask.</summary>
    public const int SectionSize = 16;

    private readonly Dictionary<long, VoxelChunk<T>> _chunks = new();

    /// <summary>Inclusive footprint of the space (optional, set by the owner).</summary>
    public Bound Bound { get; set; } = Bound.Empty;

    /// <summary>The entity at the given position, or null.</summary>
    public T? this[Int3 coords]
    {
        get
        {
            var key = ChunkKey(coords);
            return _chunks.TryGetValue(key, out var chunk) ? chunk[ChunkLocal(coords)] : default!;
        }
        set
        {
            var key = ChunkKey(coords);
            if (!_chunks.TryGetValue(key, out var chunk))
            {
                chunk = new VoxelChunk<T>(new Int2(coords.X >> 4, coords.Z >> 4));
                _chunks[key] = chunk;
            }
            chunk[ChunkLocal(coords)] = value;
        }
    }

    /// <summary>Removes all chunk storage.</summary>
    public void Clear() => _chunks.Clear();

    /// <summary>The value at the given position, or default.</summary>
    public T? GetEntityAtPoint(Int3 pos) => this[pos];

    /// <summary>Nearest entity by expanding spiral search, or null.</summary>
    public T? GetEntityAtNearest(Int3 pos)
    {
        for (var radius = 0; radius < 1000; radius++)
        {
            for (var dx = -radius; dx <= radius; dx++)
            {
                for (var dz = -radius; dz <= radius; dz++)
                {
                    if (Math.Abs(dx) != radius && Math.Abs(dz) != radius)
                        continue;

                    var candidate = this[new Int3(pos.X + dx, pos.Y, pos.Z + dz)];
                    if (candidate is not null)
                        return candidate;
                }
            }
        }
        return default!;
    }

    // ── Mapping ────────────────────────────────────────────────────────

    /// <summary>
    /// Produces a new grid with the same occupancy, each held value mapped by
    /// <paramref name="map"/>; cells whose mapped value equals default are
    /// omitted. Copies <see cref="Bound"/>.
    /// </summary>
    public VoxelLayout<TOut> Select<TOut>(Func<T, TOut> map) where TOut : notnull
    {
        var result = new VoxelLayout<TOut> { Bound = Bound };
        foreach (var (coords, value) in Cells())
        {
            var mapped = map(value);
            if (!EqualityComparer<TOut>.Default.Equals(mapped, default!))
                result[coords] = mapped;
        }
        return result;
    }

    /// <summary>Enumerates every occupied cell with its absolute coordinates.</summary>
    internal IEnumerable<(Int3 Coords, T Value)> Cells()
    {
        foreach (var chunk in _chunks.Values)
            foreach (var cell in chunk.Cells())
                yield return cell;
    }

    // ── Chunk math (SectionSize = 16, power of two) ────────────────────

    private static long ChunkKey(Int3 coords) =>
        ((long)(coords.X >> 4) << 32) | (uint)(coords.Z >> 4);

    private static Int3 ChunkLocal(Int3 coords) =>
        new(coords.X & 15, coords.Y, coords.Z & 15);
}

/// <summary>
/// An XZ chunk column of a <see cref="VoxelLayout{T}"/>: the footprint is fixed
/// at 16×16 cells; the height axis is a growable array of
/// <see cref="VoxelChunkSection{T}"/> (one per 16 cells of height). Sections are
/// created on first write, so a column's height grows by appending — never by
/// recomputing existing sections.
/// </summary>
public class VoxelChunk<T> where T : notnull
{
    private VoxelChunkSection<T>[] _sections = Array.Empty<VoxelChunkSection<T>>();

    /// <summary>Chunk column index in the XZ plane.</summary>
    public Int2 Index { get; }

    /// <summary>Sections of this column, low to high (may contain null gaps).</summary>
    public IReadOnlyList<VoxelChunkSection<T>?> Sections => _sections;

    public VoxelChunk(Int2 index) => Index = index;

    /// <summary>
    /// Chunk-local cell access: x/z in [0,16), y any (the column height).
    /// </summary>
    public T? this[Int3 local]
    {
        get
        {
            var s = local.Y >> 4;
            if (s >= _sections.Length)
                return default!;
            var section = _sections[s];
            return section is null ? default! : section[new Int3(local.X, local.Y & 15, local.Z)];
        }
        set
        {
            var s = local.Y >> 4;
            if (_sections.Length <= s)
                Array.Resize(ref _sections, s + 1);

            var section = _sections[s];
            if (section is null)
            {
                section = new VoxelChunkSection<T>();
                _sections[s] = section;
            }
            section[new Int3(local.X, local.Y & 15, local.Z)] = value;
        }
    }

    /// <summary>Enumerates every occupied cell with its absolute coordinates.</summary>
    internal IEnumerable<(Int3 Coords, T Value)> Cells()
    {
        for (var sy = 0; sy < _sections.Length; sy++)
        {
            var section = _sections[sy];
            if (section is null)
                continue;

            for (var ly = 0; ly < VoxelLayout<T>.SectionSize; ly++)
                for (var lx = 0; lx < VoxelLayout<T>.SectionSize; lx++)
                    for (var lz = 0; lz < VoxelLayout<T>.SectionSize; lz++)
                    {
                        var value = section[new Int3(lx, ly, lz)];
                        if (value is not null)
                        {
                            yield return (
                                new Int3(Index.X * VoxelLayout<T>.SectionSize + lx,
                                    sy * VoxelLayout<T>.SectionSize + ly,
                                    Index.Z * VoxelLayout<T>.SectionSize + lz),
                                value);
                        }
                    }
        }
    }
}

/// <summary>
/// A 16×16×16 paletted section of a <see cref="VoxelChunk{T}"/> column: the
/// atomic storage unit, backed by a <see cref="PalettedContainer{Int3, T}"/>
/// with a chunk strategy. Coordinates are section-local.
/// </summary>
public class VoxelChunkSection<T> where T : notnull
{
    /// <summary>Paletted cell storage of this 16×16×16 section.</summary>
    public PalettedContainer<Int3, T> Data { get; } = new(
        new Palette<T>.Int3ChunkStrategy(
            new Int3(VoxelLayout<T>.SectionSize, VoxelLayout<T>.SectionSize, VoxelLayout<T>.SectionSize),
            initialBits: 4));

    /// <summary>Section-local cell access (all components in [0,16)).</summary>
    public T? this[Int3 local]
    {
        get => Data[local];
        set => Data[local] = value;
    }
}
