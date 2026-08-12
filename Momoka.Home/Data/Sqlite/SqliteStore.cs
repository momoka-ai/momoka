using LinqToDB;
using LinqToDB.Data;
using LinqToDB.DataProvider.SQLite;
using LinqToDB.Mapping;
using Momoka.Home.Entities;
using Newtonsoft.Json;
namespace Momoka.Home.Data.Sqlite;

/// <summary>
/// SQLite-backed persistence for a residence. One store holds one open
/// connection (<see cref="DataConnection"/>) for its whole lifetime — open once,
/// call <see cref="Save"/>/<see cref="Load"/> repeatedly, then dispose. Both
/// tables are <c>Id + Json</c> (PascalCase) and every operation — schema
/// creation, inserts, deletes, queries — goes through linq2db's functional API
/// (<c>CreateTable</c>/<c>InsertOrReplace</c>/<c>Insert</c>/<c>GetTable</c>), no
/// raw SQL:
/// <list type="bullet">
/// <item><c>Residence</c> — the singleton row holding the residence serialized
/// whole via <see cref="Settings.JsonSerialization"/> (the entity registry is
/// <c>[JsonIgnore]</c> on <see cref="Residence.Entities"/> and lives below).</item>
/// <item><c>Entities</c> — one row per registered entity: id + full JSON.</item>
/// </list>
/// The voxel layout and region layer are not stored here yet — they still go
/// through <see cref="LayoutChunkCodec"/>/<see cref="RegionsCodec"/>.
/// </summary>
public sealed class SqliteStore : IDisposable
{
    private const string DbExtension = ".db";
    private readonly DataConnection _db;

    [Table("Residence")]
    private sealed class ResidenceRow
    {
        [PrimaryKey]
        public int Id { get; set; } = 1;

        [Column]
        public string Json { get; set; } = string.Empty;
    }

    [Table("Entities")]
    private sealed class EntityRow
    {
        [PrimaryKey]
        public string Id { get; set; } = string.Empty;

        [Column]
        public string Json { get; set; } = string.Empty;
    }

    /// <summary>Opens (creating if needed) the database and ensures the schema via linq.</summary>
    public SqliteStore(string dbPath)
    {
        var dir = Path.GetDirectoryName(Path.GetFullPath(dbPath));
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);
        _db = new DataConnection(new DataOptions().UseSQLite($"Data Source={dbPath}"));
        _db.CreateTable<ResidenceRow>(tableOptions: TableOptions.CreateIfNotExists);
        _db.CreateTable<EntityRow>(tableOptions: TableOptions.CreateIfNotExists);
    }

    /// <summary>Writes the residence row and its full entity registry atomically.</summary>
    public void Save(Residence residence)
    {
        using var tx = _db.BeginTransaction();
        _db.InsertOrReplace(new ResidenceRow
        {
            Id = 1,
            Json = JsonConvert.SerializeObject(residence, Settings.JsonSerialization),
        });

        // Replace the entity registry wholesale — one row per registered entity.
        _db.GetTable<EntityRow>().Delete();
        foreach (var entity in residence.Entities)
        {
            _db.Insert(new EntityRow
            {
                Id = entity.Id.ToString("D"),
                Json = JsonConvert.SerializeObject(entity, Settings.JsonSerialization),
            });
        }
        tx.Commit();
    }

    /// <summary>Loads the residence row plus its entity registry (the voxel layout is not stored yet).</summary>
    public Residence? Load()
    {
        var row = _db.GetTable<ResidenceRow>().FirstOrDefault();
        if (row is null)
            return null;
        var residence = JsonConvert.DeserializeObject<Residence>(row.Json, Settings.JsonSerialization)!;
        foreach (var entityRow in _db.GetTable<EntityRow>().OrderBy(r => r.Id))
            residence.Entities.Add(JsonConvert.DeserializeObject<Entity>(entityRow.Json, Settings.JsonSerialization)!);
        return residence;
    }

    public void Dispose() => _db.Dispose();

    /// <summary>Every residence database under <paramref name="savesRoot"/> (metadata only — entities not loaded).</summary>
    public static IReadOnlyList<Residence> ListSaves(string savesRoot)
    {
        if (!Directory.Exists(savesRoot))
            return Array.Empty<Residence>();
        var saves = new List<Residence>();
        foreach (var file in Directory.EnumerateFiles(savesRoot, "*" + DbExtension))
        {
            using var store = new SqliteStore(file);
            if (store._db.GetTable<ResidenceRow>().FirstOrDefault() is { } row)
                saves.Add(JsonConvert.DeserializeObject<Residence>(row.Json, Settings.JsonSerialization)!);
        }
        return saves;
    }

    /// <summary>Deletes a residence database file.</summary>
    public static void Delete(string dbPath)
    {
        if (File.Exists(dbPath))
            File.Delete(dbPath);
    }
}
