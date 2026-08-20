using LinqToDB;
using LinqToDB.Data;
using LinqToDB.DataProvider.SQLite;
using LinqToDB.Mapping;
using Momoka.Home.Entities;
using Momoka.Home.Entities.Properties;
using Momoka.Home.Layouts;
using Momoka.Home.Level;
using Momoka.Home.Primitives;
using Newtonsoft.Json;
namespace Momoka.Home.Data.Sqlite;

/// <summary>
/// SQLite-backed persistence for a level — one save per server（单存档；客户端选择
/// 连接不同服务器，枚举存档归云端）。One store holds one open connection
/// (<see cref="DataConnection"/>) for its whole lifetime — open once, call
/// <see cref="Save"/>/<see cref="Load"/> repeatedly, then dispose. Every
/// operation goes through linq2db's functional API (no raw SQL); the three
/// tables are written atomically in one transaction:
/// <list type="bullet">
/// <item><c>Entities</c> — one row per registered entity (incl. the hidden
/// Home entity): id + full JSON.</item>
/// <item><c>Chunks</c> — one row per non-empty voxel chunk (x, z key):
/// the chunk encoded by <see cref="LayoutChunkCodec.Encode"/> — paletted
/// sections + region spans, single source of truth for voxels.</item>
/// <item><c>RegionNames</c> — per-id region names (geometry is recomputed from
/// the spans embedded in the chunks; Region layer load deferred).</item>
/// </list>
/// </summary>
public sealed class SqliteStore : IDisposable
{
    private readonly DataConnection _db;

    [Table("Entities")]
    private sealed class EntityRow
    {
        [PrimaryKey]
        public string Id { get; set; } = string.Empty;

        [Column]
        public string Json { get; set; } = string.Empty;
    }

    [Table("Chunks")]
    private sealed class ChunkRow
    {
        [PrimaryKey]
        public int X { get; set; }

        [PrimaryKey]
        public int Z { get; set; }

        [Column]
        public byte[] Data { get; set; } = Array.Empty<byte>();
    }

    [Table("RegionNames")]
    private sealed class RegionNameRow
    {
        [PrimaryKey]
        public int Id { get; set; }

        [Column]
        public string Name { get; set; } = string.Empty;
    }

    /// <summary>Opens (creating if needed) the database and ensures the schema via linq.</summary>
    public SqliteStore(string dbPath)
    {
        var dir = Path.GetDirectoryName(Path.GetFullPath(dbPath));
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);
        _db = new DataConnection(new DataOptions().UseSQLite($"Data Source={dbPath}"));
        _db.CreateTable<EntityRow>(tableOptions: TableOptions.CreateIfNotExists);
        _db.CreateTable<ChunkRow>(tableOptions: TableOptions.CreateIfNotExists);
        _db.CreateTable<RegionNameRow>(tableOptions: TableOptions.CreateIfNotExists);
    }

    /// <summary>
    /// Writes the three tables atomically: entities (wholesale replace), voxel
    /// chunks (wholesale replace), region names (wholesale replace).
    /// </summary>
    public void Save(LevelData data)
    {
        using var tx = _db.BeginTransaction();

        // LevelData.Type 的持久化真相 = Home 实体 unit_type 属性（SQLite 无 LevelData 行）
        if (data.Entities.FirstOrDefault(e => e.Key == LevelData.HomeKey) is { } home)
            home.SetValue(Property.UnitType, data.Type);

        // Entities — one row per registered entity (incl. the hidden Home entity).
        _db.GetTable<EntityRow>().Delete();
        foreach (var entity in data.Entities)
        {
            _db.Insert(new EntityRow
            {
                Id = entity.Id.ToString("D"),
                Json = JsonConvert.SerializeObject(entity, Settings.JsonSerialization),
            });
        }

        // Chunks — one row per non-empty chunk (paletted sections + region spans).
        _db.GetTable<ChunkRow>().Delete();
        foreach (var chunk in data.Layout.Voxels.Chunks)
        {
            if (chunk.Sections.All(s => s is null || s.Data.Storage.AllZero()))
                continue; // 空 chunk 不写
            _db.Insert(new ChunkRow
            {
                X = chunk.Index.X,
                Z = chunk.Index.Z,
                Data = LayoutChunkCodec.Encode(chunk),
            });
        }

        // Region names — per-id (geometry recomputed from chunk spans on load).
        _db.GetTable<RegionNameRow>().Delete();
        var seen = new HashSet<int>();
        foreach (var chunk in data.Layout.Regions.Chunks)
            foreach (var cell in chunk.Cells())
                if (seen.Add(cell.Value.Id))
                    _db.Insert(new RegionNameRow { Id = cell.Value.Id, Name = cell.Value.Name });

        tx.Commit();
    }

    /// <summary>
    /// Loads the entity registry and rebuilds the voxel grid from chunk rows
    /// (Bound left unset — recomputed by the server's validation pass).
    /// Region layer is not restored here yet (deferred). Returns null when the
    /// store holds no level.
    /// </summary>
    public LevelData? Load()
    {
        var rows = _db.GetTable<EntityRow>().OrderBy(r => r.Id).ToList();
        if (rows.Count == 0)
            return null;

        var data = new LevelData();
        foreach (var row in rows)
            data.Entities.Add(JsonConvert.DeserializeObject<Entity>(row.Json, Settings.JsonSerialization)!);

        // Type 从 Home 实体还原（持久化真相）
        if (data.Entities.FirstOrDefault(e => e.Key == LevelData.HomeKey) is { } home)
            data.Type = home.GetValue<UnitType>(Property.UnitType);

        var byId = data.Entities.ToDictionary(e => e.Id);
        var chunks = new Dictionary<long, VoxelChunk<Entity>>();
        foreach (var row in _db.GetTable<ChunkRow>())
        {
            var decoded = LayoutChunkCodec.Decode(new Int2(row.X, row.Z), row.Data, byId);
            chunks[VoxelLayout<Entity>.ChunkKeyOf(decoded.Chunk.Index)] = decoded.Chunk;
        }
        data.Layout.Voxels = new VoxelLayout<Entity>(chunks, Bound.UnsetValue);
        return data;
    }

    public void Dispose() => _db.Dispose();

    /// <summary>Deletes the level database file.</summary>
    public static void Delete(string dbPath)
    {
        if (File.Exists(dbPath))
            File.Delete(dbPath);
    }
}
