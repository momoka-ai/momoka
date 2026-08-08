using Momoka.Home.Entities;
using Momoka.Home.Layouts;
using Momoka.Home.Primitives;
namespace Momoka.Home.Storage;

/// <summary>
/// Binary codec for the voxel layer's chunk files: each <c>Layout.{x}.{z}.dat</c>
/// stores one <see cref="VoxelChunk{T}"/>'s paletted sections — per present
/// section: the palette (entity <see cref="Entity.Id"/>s), bit width and the raw
/// packed words. Entity <see cref="Guid"/>s (not list indices) keep chunk files
/// independent of the entity list's order. Also handles directory-level
/// save/load of a whole <see cref="VoxelLayout{T}"/> into a <c>Chunks/</c>
/// folder with per-file atomic writes and stale-file cleanup.
/// </summary>
public static class LayoutChunkCodec
{
    private const string FilePrefix = "Layout.";
    private const string FileSuffix = ".dat";
    private const string ChunkPattern = "Layout.*.dat";
    private static readonly byte[] Magic = { (byte)'M', (byte)'L', (byte)'Y' };
    private const int Version = 1;

    /// <summary>The chunk file name for an XZ chunk index (e.g. <c>Layout.0.0.dat</c>).</summary>
    public static string FileName(Int2 chunkIndex) =>
        $"{FilePrefix}{chunkIndex.X}.{chunkIndex.Z}{FileSuffix}";

    // ── Single chunk ────────────────────────────────────

    /// <summary>Encodes one chunk's paletted sections to binary.</summary>
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
            writer.Write(sy);

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

    /// <summary>Decodes one chunk's paletted sections from binary, resolving palette entities by id.</summary>
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
        var maxSy = 0;
        for (var i = 0; i < sectionCount; i++)
        {
            var sy = reader.ReadInt32();

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
            if (sy > maxSy)
                maxSy = sy;
        }

        var sections = new VoxelChunkSection<Entity>?[maxSy + 1];
        foreach (var (sy, section) in sectionBySy)
            sections[sy] = section;
        return new VoxelChunk<Entity>(index, sections);
    }

    // ── Whole layout / directory ────────────────────────

    /// <summary>
    /// Saves every non-empty chunk as <c>Layout.{x}.{z}.dat</c> (atomic per
    /// file) and deletes stale chunk files no longer present in the layout.
    /// </summary>
    public static void Save(VoxelLayout<Entity> layout, string chunksDir)
    {
        Directory.CreateDirectory(chunksDir);

        var current = new HashSet<string>();
        foreach (var chunk in layout.Chunks)
        {
            if (chunk.Sections.All(s => s is null || s.Data.Storage.AllZero()))
                continue;

            var name = FileName(chunk.Index);
            current.Add(name);
            var path = Path.Combine(chunksDir, name);
            var tmp = path + ".tmp";
            File.WriteAllBytes(tmp, Encode(chunk));
            File.Move(tmp, path, overwrite: true);
        }

        foreach (var file in Directory.EnumerateFiles(chunksDir, ChunkPattern))
            if (!current.Contains(Path.GetFileName(file)))
                File.Delete(file);
    }

    /// <summary>
    /// Loads every chunk file, resolving palette entities from
    /// <paramref name="entities"/>. The layout's <see cref="VoxelLayout{T}.Bound"/>
    /// is not stored in chunk files — the caller restores it from level metadata.
    /// </summary>
    public static VoxelLayout<Entity> Load(string chunksDir, IReadOnlyList<Entity> entities)
    {
        var chunks = new Dictionary<long, VoxelChunk<Entity>>();
        if (Directory.Exists(chunksDir))
        {
            var byId = entities.ToDictionary(e => e.Id);
            foreach (var file in Directory.EnumerateFiles(chunksDir, ChunkPattern))
            {
                var index = ParseIndex(Path.GetFileNameWithoutExtension(file));
                chunks[VoxelLayout<Entity>.ChunkKeyOf(index)] = Decode(index, File.ReadAllBytes(file), byId);
            }
        }
        return new VoxelLayout<Entity>(chunks, Bound.Empty);
    }

    private static Palette<Entity>.Int3ChunkStrategy NewStrategy() => new(
        new Int3(VoxelLayout<Entity>.SectionSize, VoxelLayout<Entity>.SectionSize, VoxelLayout<Entity>.SectionSize),
        initialBits: 4);

    private static Int2 ParseIndex(string nameWithoutExtension)
    {
        // "Layout.0.0" → chunk (0, 0)
        var parts = nameWithoutExtension.Split('.');
        if (parts.Length != 3 || !int.TryParse(parts[1], out var x) || !int.TryParse(parts[2], out var z))
            throw new InvalidDataException($"Invalid chunk file name '{nameWithoutExtension}'.");
        return new Int2(x, z);
    }
}
