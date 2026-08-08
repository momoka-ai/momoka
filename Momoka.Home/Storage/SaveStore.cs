using Momoka.Home.Entities;
using Momoka.Home.Primitives;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;
namespace Momoka.Home.Storage;

/// <summary>
/// Folder-based store for residence saves. Each save is a directory
/// <c>Saves/&lt;Name&gt;/</c> holding:
/// <list type="bullet">
/// <item><c>Residence.json</c> — identity + bound (this Save's metadata).</item>
/// <item><c>Entities.json</c> — the flattened entity snapshot (<see cref="EntitiesCodec"/>).</item>
/// <item><c>Chunks/Layout.{x}.{z}.dat</c> — voxel sections + region spans (<see cref="LayoutChunkCodec"/>).</item>
/// <item><c>Regions.json</c> — human-editable region names (<see cref="RegionsCodec"/>).</item>
/// </list>
/// </summary>
public static class SaveStore
{
    public const string ResidenceFile = "Residence.json";
    public const string EntitiesFile = "Entities.json";
    public const string RegionsFile = "Regions.json";

    private static readonly JsonSerializerSettings Settings = new()
    {
        ContractResolver = new DefaultContractResolver { NamingStrategy = new SnakeCaseNamingStrategy() },
        Formatting = Formatting.Indented,
        Converters = { new StringEnumConverter { NamingStrategy = new SnakeCaseNamingStrategy() } },
    };

    private sealed class ResidenceMetadata
    {
        public int Version { get; set; } = 1;
        public string Name { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public UnitType Type { get; set; }
        public Int3? BoundMin { get; set; }
        public Int3? BoundMax { get; set; }
        public string ChunkLayout { get; set; } = "Chunks";
    }

    /// <summary>Every save under <paramref name="savesRoot"/> (metadata only — grids are not loaded).</summary>
    public static IReadOnlyList<Save> ListSaves(string savesRoot)
    {
        if (!Directory.Exists(savesRoot))
            return Array.Empty<Save>();
        var saves = new List<Save>();
        foreach (var dir in Directory.EnumerateDirectories(savesRoot))
        {
            var path = Path.Combine(dir, ResidenceFile);
            if (!File.Exists(path))
                continue;
            var dto = ReadResidence(path);
            saves.Add(new Save
            {
                Path = dir,
                Name = dto.Name,
                Address = dto.Address,
                Type = dto.Type,
                Bound = ReadBound(dto),
            });
        }
        return saves;
    }

    /// <summary>Loads a save folder fully: metadata, entities, voxel grid and region layer.</summary>
    public static Save Load(string saveDir)
    {
        var dto = ReadResidence(Path.Combine(saveDir, ResidenceFile));
        var entities = EntitiesCodec.Deserialize(File.ReadAllText(Path.Combine(saveDir, EntitiesFile)));
        var loaded = LayoutChunkCodec.Load(Path.Combine(saveDir, dto.ChunkLayout), entities);

        var save = new Save
        {
            Path = saveDir,
            Name = dto.Name,
            Address = dto.Address,
            Type = dto.Type,
            Bound = ReadBound(dto),
            Grid = loaded.Grid,
            Regions = RegionsCodec.Load(loaded.RegionColumns, Path.Combine(saveDir, RegionsFile)),
        };
        foreach (var entity in entities)
            save.Entities[entity.Id] = entity;
        return save;
    }

    /// <summary>Writes a residence as a save folder under <paramref name="savesRoot"/>.</summary>
    public static void Save(Residence residence, string savesRoot)
    {
        var saveDir = Path.Combine(savesRoot, SanitizeFolder(residence.Name));
        Directory.CreateDirectory(saveDir);

        var grid = residence.Layout.Layout;
        var dto = new ResidenceMetadata
        {
            Name = residence.Name,
            Address = residence.Address,
            Type = residence.Type,
        };
        if (!grid.Bound.IsEmpty)
        {
            dto.BoundMin = grid.Bound.Min;
            dto.BoundMax = grid.Bound.Max;
        }
        File.WriteAllText(Path.Combine(saveDir, ResidenceFile), JsonConvert.SerializeObject(dto, Settings));
        File.WriteAllText(Path.Combine(saveDir, EntitiesFile), EntitiesCodec.Serialize(residence.Entities));
        LayoutChunkCodec.Save(grid, residence.Layout.Regions, Path.Combine(saveDir, dto.ChunkLayout));
        if (residence.Layout.Regions is { } regions)
            RegionsCodec.Save(regions, Path.Combine(saveDir, RegionsFile));
    }

    /// <summary>Deletes a save folder recursively.</summary>
    public static void Delete(string saveDir)
    {
        if (Directory.Exists(saveDir))
            Directory.Delete(saveDir, recursive: true);
    }

    private static ResidenceMetadata ReadResidence(string path) =>
        JsonConvert.DeserializeObject<ResidenceMetadata>(File.ReadAllText(path), Settings) ?? new ResidenceMetadata();

    private static Bound ReadBound(ResidenceMetadata dto) =>
        dto.BoundMin is { } min && dto.BoundMax is { } max ? Bound.FromCorners(min, max) : Bound.Empty;

    /// <summary>Replaces path-hostile characters in a residence name for use as a folder name.</summary>
    private static string SanitizeFolder(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var chars = name.Select(c => invalid.Contains(c) ? '_' : c).ToArray();
        var folder = new string(chars).Trim();
        return folder.Length == 0 ? "Save" : folder;
    }
}
