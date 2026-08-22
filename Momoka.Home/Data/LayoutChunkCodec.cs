using Momoka.Home.Levels;
using Momoka.Home.Levels.Entities;
using Momoka.Home.Levels.Layouts;
using Momoka.Home.Primitives;
namespace Momoka.Home.Data;

/// <summary>One column's region spans inside a chunk file (world XZ column).</summary>
public readonly record struct ChunkRegionColumn(Int2 World, RegionSpan[] Spans);

/// <summary>A half-open Y interval [Y0, Y1) tagged with a region id.</summary>
public readonly record struct RegionSpan(int Y0, int Y1, int RegionId);

/// <summary>A decoded chunk: the voxel chunk plus its region columns.</summary>
public readonly record struct DecodedChunk(VoxelChunk<Entity> Chunk, IReadOnlyList<ChunkRegionColumn> RegionColumns);

/// <summary>
/// Binary codec for the voxel layer's chunk payloads (stored in the SQLite
/// <c>Chunks</c> table): each chunk's paletted sections — per present section:
/// the palette (entity <see cref="Entity.Id"/>s), bit width and the raw packed
/// words. Entity <see cref="Guid"/>s (not list indices) keep chunk payloads
/// independent of the entity list's order. Region spans ride along inside the
/// chunk payload (single source of truth; geometry recomputed on load).
/// </summary>
public static class LayoutChunkCodec
{
    private static readonly byte[] Magic = { (byte)'M', (byte)'L', (byte)'Y' };
    private const int Version = 1;

    // ── Single chunk ────────────────────────────────────

    /// <summary>
    /// Encodes one chunk's paletted sections plus its region columns to binary
    /// (region columns are the spans of this chunk's 16×16 XZ footprint).
    /// </summary>
    public static byte[] Encode(VoxelChunk<Entity> chunk, IReadOnlyList<ChunkRegionColumn>? regionColumns = null)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);

        writer.Write(Magic);
        writer.Write(Version);

        var sections = chunk.Sections;
        var present = new List<int>(sections.Count);
        for (var sy = 0; sy < sections.Count; sy++)
            if (sections[sy] is not null)
                present.Add(sy);

        writer.Write(present.Count);
        foreach (var sy in present)
        {
            var section = sections[sy]!;
            writer.Write(sy + chunk.BaseSy); // 世界 section Y（负 Y 支持；旧文件全非负时与数组索引相同）

            // Palette values, skipping the reserved empty slot at index 0.
            var palette = section.Data.Palette.Values;
            writer.Write(palette.Count - 1);
            for (var i = 1; i < palette.Count; i++)
                writer.Write(palette[i].Id.ToByteArray());

            var storage = section.Data.Storage;
            writer.Write(storage.Size);
            writer.Write(storage.Bits);
            var data = storage.Data;
            writer.Write(data.Length);
            foreach (var word in data)
                writer.Write(word);
        }

        // Region columns of this chunk's 16×16 footprint (stored as chunk-local indices).
        if (regionColumns is null)
        {
            writer.Write(0);
        }
        else
        {
            writer.Write(regionColumns.Count);
            foreach (var column in regionColumns)
            {
                var lx = column.World.X - chunk.Index.X * VoxelLayout<Entity>.SectionSize;
                var lz = column.World.Z - chunk.Index.Z * VoxelLayout<Entity>.SectionSize;
                writer.Write(lz * VoxelLayout<Entity>.SectionSize + lx);
                writer.Write(column.Spans.Length);
                foreach (var span in column.Spans)
                {
                    writer.Write(span.Y0);
                    writer.Write(span.Y1);
                    writer.Write(span.RegionId);
                }
            }
        }
        return stream.ToArray();
    }

    /// <summary>
    /// Decodes one chunk's paletted sections + region columns from binary, resolving
    /// palette entities by id.
    /// </summary>
    public static DecodedChunk Decode(Int2 index, byte[] data, IReadOnlyDictionary<Guid, Entity> entities)
    {
        using var reader = new BinaryReader(new MemoryStream(data));

        if (!reader.ReadBytes(Magic.Length).SequenceEqual(Magic))
            throw new InvalidDataException("Not a Momoka layout chunk file.");
        var version = reader.ReadInt32();
        if (version != Version)
            throw new InvalidDataException($"Unsupported chunk format version {version}.");

        var sectionCount = reader.ReadInt32();
        var sectionBySy = new Dictionary<int, VoxelChunkSection<Entity>>();
        var minSy = int.MaxValue;
        var maxSy = int.MinValue;
        for (var i = 0; i < sectionCount; i++)
        {
            var sy = reader.ReadInt32(); // 世界 section Y

            var paletteCount = reader.ReadInt32();
            var paletteValues = new Entity[paletteCount];
            for (var j = 0; j < paletteCount; j++)
            {
                var id = new Guid(reader.ReadBytes(16));
                if (!entities.TryGetValue(id, out var entity))
                    throw new InvalidDataException($"Chunk {index} references unknown entity '{id}'.");
                paletteValues[j] = entity;
            }

            var size = reader.ReadInt32();
            var bits = reader.ReadInt32();
            var wordCount = reader.ReadInt32();
            var words = new ulong[wordCount];
            for (var k = 0; k < wordCount; k++)
                words[k] = reader.ReadUInt64();

            var container = new PalettedContainer<Int3, Entity>(
                NewStrategy(), Palette<Entity>.FromValues(paletteValues), new PackedBitStorage(size, bits, words));
            sectionBySy[sy] = new VoxelChunkSection<Entity>(container);
            if (sy < minSy)
                minSy = sy;
            if (sy > maxSy)
                maxSy = sy;
        }

        var sections = new VoxelChunkSection<Entity>?[maxSy - minSy + 1];
        foreach (var (sy, section) in sectionBySy)
            sections[sy - minSy] = section;

        var regionColumnCount = reader.ReadInt32();
        var regionColumns = new ChunkRegionColumn[regionColumnCount];
        for (var i = 0; i < regionColumnCount; i++)
        {
            var local = reader.ReadInt32();
            var spanCount = reader.ReadInt32();
            var spans = new RegionSpan[spanCount];
            for (var j = 0; j < spanCount; j++)
                spans[j] = new RegionSpan(reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt32());

            var world = new Int2(
                index.X * VoxelLayout<Entity>.SectionSize + local % VoxelLayout<Entity>.SectionSize,
                index.Z * VoxelLayout<Entity>.SectionSize + local / VoxelLayout<Entity>.SectionSize);
            regionColumns[i] = new ChunkRegionColumn(world, spans);
        }

        return new DecodedChunk(new VoxelChunk<Entity>(index, sections, minSy), regionColumns);
    }

    // ── Region columns ─────────────────────────────────

    /// <summary>Extracts the region spans of a chunk's 16×16 footprint from the global region layer.</summary>
    public static IReadOnlyList<ChunkRegionColumn> ExtractRegionColumns(VoxelChunk<Entity> chunk, ColumnLayout<Region> regions)
    {
        var byColumn = new Dictionary<long, List<(int Y, Region R)>>();
        foreach (var (pos, region) in regions.Cells())
        {
            if ((pos.X >> 4) != chunk.Index.X || (pos.Z >> 4) != chunk.Index.Z)
                continue;
            var key = (long)pos.Z << 32 | (uint)pos.X;
            if (!byColumn.TryGetValue(key, out var ys))
                byColumn[key] = ys = new List<(int, Region)>();
            ys.Add((pos.Y, region));
        }

        var columns = new List<ChunkRegionColumn>();
        foreach (var (key, ys) in byColumn)
        {
            ys.Sort((a, b) => a.Y.CompareTo(b.Y));
            var spans = new List<RegionSpan>();
            var runY0 = ys[0].Y;
            var runRegion = ys[0].R;
            var prevY = ys[0].Y;
            for (var i = 1; i < ys.Count; i++)
            {
                var (y, r) = ys[i];
                if (r == runRegion && y == prevY + 1)
                {
                    prevY = y;
                    continue;
                }
                spans.Add(new RegionSpan(runY0, prevY + 1, runRegion.Id));
                runY0 = y;
                runRegion = r;
                prevY = y;
            }
            spans.Add(new RegionSpan(runY0, prevY + 1, runRegion.Id));
            columns.Add(new ChunkRegionColumn(new Int2((int)(key & uint.MaxValue), (int)(key >> 32)), spans.ToArray()));
        }
        return columns;
    }

    private static Palette<Entity>.Int3ChunkStrategy NewStrategy() => new(
        new Int3(VoxelLayout<Entity>.SectionSize, VoxelLayout<Entity>.SectionSize, VoxelLayout<Entity>.SectionSize),
        initialBits: 4);
}
