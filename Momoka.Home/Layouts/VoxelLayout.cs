using System.Collections;
using System.Numerics;
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
    public const int SectionSize = 16;

    private readonly Dictionary<long, VoxelChunk<T>> _chunks = new();

    public Bound Bound { get; set; } = Bound.UnsetValue;
    public float Length { get; set; } = 10f;

    /// <summary>Maps a world position (cm) to the nearest cell index using this grid's <see cref="Length"/>.</summary>
    public Int3 GetAsRelative(Float3 world) =>
        new(ToCell(world.X), ToCell(world.Y), ToCell(world.Z));

    /// <summary>Maps a cell index to the world position (cm) of its origin.</summary>
    public Float3 GetAsAbsolute(Int3 cell) =>
        new(cell.X * Length, cell.Y * Length, cell.Z * Length);

    private int ToCell(float v) =>
        (int)Math.Round(v / Length, MidpointRounding.AwayFromZero);

    /// <summary>The entity at the given position, or null.</summary>
    public T? this[Int3 pos]
    {
        get
        {
            if (!Bound.IsValid(GetAsAbsolute(pos)))
            {
                return default;
            }
            var key = ChunkKey(pos);
            return _chunks.TryGetValue(key, out var chunk) ? chunk[ChunkLocal(pos)] : default!;
        }
        set
        {
            if (!Bound.IsValid(GetAsAbsolute(pos)))
            {
                return;
            }
            var key = ChunkKey(pos);
            if (!_chunks.TryGetValue(key, out var chunk))
            {
                chunk = new VoxelChunk<T>(new Int2(pos.X >> 4, pos.Z >> 4));
                _chunks[key] = chunk;
            }
            chunk[ChunkLocal(pos)] = value;
        }
    }

    public T? this[Float3 pos]
    {
        get => this[GetAsRelative(pos)];
        set => this[GetAsRelative(pos)] = value;
    }

    /// <summary>Removes all chunk storage.</summary>
    public void Clear() => _chunks.Clear();

    /// <summary>The value at the given position, or default.</summary>
    public T? AtPoint(Int3 pos) => this[pos];

    public T? AtPoint(int x, int y, int z) => this[new Int3(x, y, z)];

    /// <summary>Nearest entity by expanding spiral search, or null.</summary>
    public T? AtNearest(Int3 pos)
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

    public VoxelLayout<TOut> Select<TOut>(Func<T, TOut> map) where TOut : notnull
    {
        var result = new VoxelLayout<TOut> { Bound = Bound, Length = Length };
        foreach (var (coords, value) in Cells())
        {
            var mapped = map(value);
            if (!EqualityComparer<TOut>.Default.Equals(mapped, default!))
                result[coords] = mapped;
        }
        return result;
    }

    internal IEnumerable<(Int3 Coords, T Value)> Cells()
    {
        foreach (var chunk in _chunks.Values)
            foreach (var cell in chunk.Cells())
                yield return cell;
    }

    public VoxelIterator<T> GetIteratorAt(int x, int z) => new(this, x, z);

    public VoxelLayout() { }

    internal IEnumerable<VoxelChunk<T>> Chunks => _chunks.Values;

    internal VoxelLayout(Dictionary<long, VoxelChunk<T>> chunks, Bound bound)
    {
        _chunks = chunks;
        Bound = bound;
    }

    internal static long ChunkKeyOf(Int2 index) =>
        ((long)index.X << 32) | (uint)index.Z;

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
    private VoxelChunkSection<T>?[] _sections = Array.Empty<VoxelChunkSection<T>>();
    private int _baseSy; // 数组索引 0 对应的世界 section Y（可为负——负 Y 格支持）

    public Int2 Index { get; }
    public IReadOnlyList<VoxelChunkSection<T>?> Sections => _sections;
    internal int BaseSy => _baseSy;

    public VoxelChunk(Int2 index) => Index = index;

    /// <summary>Restores a chunk from serialized sections (may contain null gaps).
    /// 数组索引 0 对应世界 section Y = 0（负 Y 数据请用内部构造恢复）。</summary>
    public VoxelChunk(Int2 index, VoxelChunkSection<T>?[] sections)
        : this(index, sections, 0) { }

    /// <summary>内部构造：sections 数组索引 0 对应世界 section <paramref name="baseSy"/>（可为负）。</summary>
    internal VoxelChunk(Int2 index, VoxelChunkSection<T>?[] sections, int baseSy)
    {
        Index = index;
        _sections = sections;
        _baseSy = baseSy;
    }

    /// <summary>
    /// Chunk-local cell access: x/z in [0,16), y any (the column height,
    /// including negative — sections below the array head are empty).
    /// </summary>
    public T? this[Int3 local]
    {
        get
        {
            var i = (local.Y >> 4) - _baseSy;
            if (i < 0 || i >= _sections.Length)
                return default!;
            var section = _sections[i];
            return section is null ? default! : section[new Int3(local.X, local.Y & 15, local.Z)];
        }
        set
        {
            var s = local.Y >> 4;
            var i = s - _baseSy;
            if (i < 0)
            {
                // 头部扩展：向负方向增长的 section 索引——新数组平移，baseSy 下移
                var grow = -i;
                var shifted = new VoxelChunkSection<T>?[_sections.Length + grow];
                Array.Copy(_sections, 0, shifted, grow, _sections.Length);
                _sections = shifted;
                _baseSy = s;
                i = 0;
            }
            else if (i >= _sections.Length)
            {
                Array.Resize(ref _sections, i + 1);
            }

            var section = _sections[i];
            if (section is null)
            {
                section = new VoxelChunkSection<T>();
                _sections[i] = section;
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

            var last = VoxelLayout<T>.SectionSize - 1;
            foreach (var local in Int3.Range(Int3.Zero, new Int3(last, last, last)))
            {
                var value = section[local];
                if (value is not null)
                {
                    yield return (
                        new Int3(Index.X * VoxelLayout<T>.SectionSize + local.X,
                            (_baseSy + sy) * VoxelLayout<T>.SectionSize + local.Y,
                            Index.Z * VoxelLayout<T>.SectionSize + local.Z),
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
    public static readonly Float3 Size = new(16.0f);
    public PalettedContainer<Int3, T> Data { get; }

    public VoxelChunkSection()
        : this(new PalettedContainer<Int3, T>(new Palette<T>.Int3ChunkStrategy(
        size: Size.AsInt3(),
        initialBits: 4)))
    { }

    /// <summary>Restores a section from serialized storage.</summary>
    internal VoxelChunkSection(PalettedContainer<Int3, T> data) => Data = data;

    /// <summary>Section-local cell access (all components in [0,16)).</summary>
    public T? this[Int3 p]
    {
        get => Data[p];
        set => Data[p] = value;
    }
}

/// <summary>
/// A vertical column cursor over a <see cref="VoxelLayout{T}"/>: for a fixed XZ
/// coordinate it enumerates every cell from the bound's bottom cell to its top
/// cell, yielding an <c>(int Y, T? Value)</c> tuple per step — <c>Value</c> is
/// <c>default</c> for cells with no stored value — the Minecraft
/// <c>BlockIterator</c> analogue. Implements
/// <c>IEnumerable&lt;(int Y, T? Value)&gt;</c> so it composes with foreach and
/// LINQ; each enumeration walks the column fresh.
/// </summary>
public class VoxelIterator<T> : IEnumerable<(int Y, T? Value)> where T : notnull
{
    public VoxelLayout<T> Source { get; }
    public int X { get; }
    public int Z { get; }
    public int MinY { get; }
    public int MaxY { get; }

    public VoxelIterator(VoxelLayout<T> source, Int2 p) : this(source, p.X, p.Z) { }

    public VoxelIterator(VoxelLayout<T> source, Int3 p) : this(source, p.X, p.Z) { }

    public VoxelIterator(VoxelLayout<T> source, int x, int z)
    {
        Source = source;
        X = x;
        Z = z;
        MinY = source.Bound.Valid ? (int)Math.Floor(source.Bound.Min.Y / source.Length) : 1;
        MaxY = source.Bound.Valid ? (int)Math.Floor(source.Bound.Max.Y / source.Length) : 0;
    }

    /// <summary>Enumerates every cell of the column, bottom to top, as <c>(Y, Value)</c> — empty cells yield <c>default</c>.</summary>
    public IEnumerator<(int Y, T? Value)> GetEnumerator()
    {
        for (var y = MinY; y <= MaxY; y++)
            yield return (y, Source[new Int3(X, y, Z)]);
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}