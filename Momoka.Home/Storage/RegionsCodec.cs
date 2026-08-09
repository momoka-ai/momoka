using Momoka.Home.Layouts;
using Momoka.Home.Primitives;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
namespace Momoka.Home.Storage;

/// <summary>
/// JSON codec for <c>Regions.json</c> — the human-editable per-id region names.
/// Region geometry (bounds / volume / area) is deliberately NOT stored here: it
/// is recomputed on load from the region spans embedded in the chunk files, so
/// the spans stay the single source of truth and the JSON can never drift from
/// the voxel layer.
/// </summary>
public static class RegionsCodec
{
    private sealed class RegionsFile
    {
        public int Version { get; set; } = 1;
        public List<RegionEntry> Regions { get; set; } = new();
    }

    private sealed class RegionEntry
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    private static readonly JsonSerializerSettings Settings = new()
    {
        ContractResolver = new DefaultContractResolver { NamingStrategy = new SnakeCaseNamingStrategy() },
        Formatting = Formatting.Indented,
    };

    /// <summary>Writes the id → name pairs of every region in <paramref name="regions"/> to <paramref name="path"/>.</summary>
    public static void Save(ColumnLayout<Region> regions, string path)
    {
        var file = new RegionsFile();
        var seen = new HashSet<int>();
        foreach (var (_, _, span) in regions.AllSpans())
        {
            if (seen.Add(span.Value.Id))
                file.Regions.Add(new RegionEntry { Id = span.Value.Id, Name = span.Value.Name });
        }
        file.Regions.Sort((a, b) => a.Id.CompareTo(b.Id));
        File.WriteAllText(path, JsonConvert.SerializeObject(file, Settings));
    }

    /// <summary>
    /// Rebuilds the region layer from the region columns carried by the chunk
    /// files, applying the names in <paramref name="regionsFile"/> (missing →
    /// default <c>"Region {id}"</c>). Returns an empty layout when there are no
    /// region columns.
    /// </summary>
    public static ColumnLayout<Region> Load(IReadOnlyList<ChunkRegionColumn> columns, string? regionsFile)
    {
        if (columns.Count == 0)
            return ColumnLayout<Region>.Empty();

        var names = new Dictionary<int, string>();
        if (regionsFile is not null && File.Exists(regionsFile))
        {
            var file = JsonConvert.DeserializeObject<RegionsFile>(File.ReadAllText(regionsFile), Settings);
            if (file is not null)
            {
                foreach (var entry in file.Regions)
                    names[entry.Id] = entry.Name;
            }
        }

        var minX = int.MaxValue;
        var minZ = int.MaxValue;
        var maxX = int.MinValue;
        var maxZ = int.MinValue;
        var perColumn = new Dictionary<long, List<RegionSpan>>();
        foreach (var column in columns)
        {
            minX = Math.Min(minX, column.World.X);
            minZ = Math.Min(minZ, column.World.Z);
            maxX = Math.Max(maxX, column.World.X);
            maxZ = Math.Max(maxZ, column.World.Z);
            perColumn[ColumnKey(column.World.X, column.World.Z)] = column.Spans.ToList();
        }

        // Pass 1 — per-region stats from the spans (mirrors Region.Aggregate).
        var stats = new Dictionary<int, MutableStats>();
        foreach (var column in columns)
        {
            foreach (var span in column.Spans)
            {
                if (!stats.TryGetValue(span.RegionId, out var s))
                {
                    s = new MutableStats();
                    stats[span.RegionId] = s;
                }
                if (column.World.X < s.MinX) s.MinX = column.World.X;
                if (column.World.Z < s.MinZ) s.MinZ = column.World.Z;
                if (column.World.X > s.MaxX) s.MaxX = column.World.X;
                if (column.World.Z > s.MaxZ) s.MaxZ = column.World.Z;
                if (span.Y0 < s.MinY) s.MinY = span.Y0;
                if (span.Y1 - 1 > s.MaxY) s.MaxY = span.Y1 - 1;
                s.Volume += span.Y1 - span.Y0;
                s.Footprints.Add(ColumnKey(column.World.X, column.World.Z));
            }
        }

        var byId = new Dictionary<int, Region>();
        foreach (var (id, s) in stats)
        {
            var bounds = Bound.FromCorners(new Int3(s.MinX, s.MinY, s.MinZ).ToFloat3(), new Int3(s.MaxX, s.MaxY, s.MaxZ).ToFloat3());
            var name = names.TryGetValue(id, out var n) ? n : $"Region {id}";
            byId[id] = new Region(id, bounds, s.Volume, s.Footprints.Count) { Name = name };
        }

        // Pass 2 — flatten the per-column spans into the layout's storage.
        var width = maxX - minX + 1;
        var depth = maxZ - minZ + 1;
        var spans = new List<ColumnLayout<Region>.Span>(columns.Count);
        var offsets = new int[width * depth + 1];
        for (var z = 0; z < depth; z++)
        {
            for (var x = 0; x < width; x++)
            {
                if (perColumn.TryGetValue(ColumnKey(minX + x, minZ + z), out var columnSpans))
                {
                    foreach (var span in columnSpans)
                        spans.Add(new ColumnLayout<Region>.Span(span.Y0, span.Y1, byId[span.RegionId]));
                }
                offsets[z * width + x + 1] = spans.Count;
            }
        }
        return new ColumnLayout<Region>(width, depth, spans.ToArray(), offsets);
    }

    private static long ColumnKey(int x, int z) => (long)z << 32 | (uint)x;

    private sealed class MutableStats
    {
        public int MinX = int.MaxValue, MinY = int.MaxValue, MinZ = int.MaxValue;
        public int MaxX = int.MinValue, MaxY = int.MinValue, MaxZ = int.MinValue;
        public long Volume;
        public readonly HashSet<long> Footprints = new();
    }
}
