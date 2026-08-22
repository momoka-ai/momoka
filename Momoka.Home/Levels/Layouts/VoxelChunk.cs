using Momoka.Home.Primitives;
namespace Momoka.Home.Levels.Layouts;

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
