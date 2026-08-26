using Momoka.Home.Levels.Entities;
using Momoka.Home.Levels.Layouts;
using Momoka.Home.Primitives;
namespace Momoka.Home.Data;

/// <summary>
/// Binary codec for the voxel layer's chunk payloads (stored in the SQLite
/// <c>Chunks</c> table): each chunk's paletted sections — per present section:
/// the palette (entity <see cref="Entity.Id"/>s), bit width and the raw packed
/// words. Entity <see cref="Guid"/>s (not list indices) keep chunk payloads
/// independent of the entity list's order.
/// </summary>
public static class LayoutChunkCodec
{
    private static readonly byte[] Magic = { (byte)'M', (byte)'L', (byte)'Y' };
    private const int Version = 1;

    // ── Single chunk ────────────────────────────────────

    /// <summary>
    /// Encodes one chunk's paletted sections to binary.
    /// </summary>
    public static byte[] Encode(VoxelChunk<Entity> chunk)
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
        return stream.ToArray();
    }

    /// <summary>
    /// Decodes one chunk's paletted sections from binary, resolving palette
    /// entities by id.
    /// </summary>
    public static VoxelChunk<Entity> Decode(Int2 index, byte[] data, IReadOnlyDictionary<Guid, Entity> entities)
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

        return new VoxelChunk<Entity>(index, sections, minSy);
    }

    private static Palette<Entity>.Int3ChunkStrategy NewStrategy() => new(
        new Int3(VoxelLayout<Entity>.SectionSize, VoxelLayout<Entity>.SectionSize, VoxelLayout<Entity>.SectionSize),
        initialBits: 4);
}
