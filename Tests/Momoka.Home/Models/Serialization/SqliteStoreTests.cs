using Xunit;
using Momoka.Home.Entities;
using Momoka.Home.Data.Sqlite;
using Momoka.Home.Geometry;
using Momoka.Home.Primitives;
namespace Momoka.Home.Tests.Models.Serialization;

/// <summary>
/// SqliteStore round-trips the residence-level data: a single-file database
/// holding the singleton <c>Residence</c> row (mapped directly onto
/// <see cref="Residence"/>) and one <c>Entities</c> row per registered entity.
/// The store holds one open connection for its lifetime; the voxel layout is
/// not stored yet — only the entity registry.
/// </summary>
public class SqliteStoreTests
{
    private static Entity Box(string path, int sx, int sy, int sz) => new()
    {
        Key = new Key(path),
        Volume = new Box3D { SizeX = sx, SizeY = sy, SizeZ = sz },
    };

    private static Residence DemoResidence()
    {
        var residence = new Residence
        {
            Name = "Demo Home",
            Address = "1 Sunshine Ave",
            Type = UnitType.House,
            Bound = new Bound(new Float3(0, 0, 0), new Float3(100, 300, 100)),
        };
        residence.Components.Add(new DataSource(DataSourceType.Temperature) { Value = 24.5f });

        var lamp = Box("lamp", 1, 1, 1);
        lamp.Transform = new Transform(new Float3(20, 10, 30), Rotation.Identity);
        lamp.AddProperties(new[] { new BooleanProperty(Property.IsImmutable, true) });

        var ac = Box("ac", 2, 1, 1);
        ac.Transform = new Transform(new Float3(40, 20, 50), Rotation.Identity);
        ac.AddComponent(new CommandTarget { Commands = "[\"turn_on\",\"turn_off\"]" });

        residence.Entities.AddRange(new[] { lamp, ac });
        return residence;
    }

    private static string TempDb() =>
        Path.Combine(Path.GetTempPath(), "momoka_sqlite_" + Guid.NewGuid().ToString("N") + ".db");

    [Fact]
    public void SaveLoad_RoundTripsResidenceAndEntities()
    {
        var residence = DemoResidence();
        var dbPath = TempDb();
        try
        {
            using (var store = new SqliteStore(dbPath))
            {
                store.Save(residence);
                Assert.True(File.Exists(dbPath));

                var loaded = store.Load();
                Assert.NotNull(loaded);
                Assert.Equal(residence.Name, loaded!.Name);
                Assert.Equal(residence.Address, loaded.Address);
                Assert.Equal(residence.Type, loaded.Type);
                Assert.Equal(residence.Bound, loaded.Bound);
                Assert.Equal(residence.Components.Count, loaded.Components.Count);
                Assert.Equal(residence.Entities.Count, loaded.Entities.Count);

                var source = Assert.Single(loaded.Components.OfType<DataSource>());
                Assert.Equal(24.5f, source.Value);

                foreach (var original in residence.Entities)
                {
                    var rebuilt = Assert.Single(loaded.Entities, e => e.Id == original.Id);
                    Assert.Equal(original.Key, rebuilt.Key);
                    Assert.Equal(original.Transform, rebuilt.Transform);
                    Assert.Equal(original.Volume.GetType(), rebuilt.Volume.GetType());
                    Assert.Equal(original.Properties.Count, rebuilt.Properties.Count);
                    Assert.Equal(original.Components.Count, rebuilt.Components.Count);
                    if (original.Components.Count > 0)
                        Assert.Equal(original.Components[0].GetType(), rebuilt.Components[0].GetType());
                }
            }
        }
        finally
        {
            SqliteStore.Delete(dbPath);
        }
    }

    [Fact]
    public void Save_Twice_KeepsSingleResidenceRow()
    {
        var residence = DemoResidence();
        var dbPath = TempDb();
        try
        {
            using (var store = new SqliteStore(dbPath))
            {
                store.Save(residence);
                store.Save(residence);

                var loaded = store.Load();
                Assert.NotNull(loaded);
                Assert.Equal("Demo Home", loaded!.Name);
                Assert.Equal(residence.Entities.Count, loaded.Entities.Count);
            }
            Assert.Single(SqliteStore.ListSaves(Path.GetDirectoryName(dbPath)!));
        }
        finally
        {
            SqliteStore.Delete(dbPath);
        }
    }

    [Fact]
    public void ListSaves_ReturnsMetadataOnly()
    {
        var residence = DemoResidence();
        var root = Path.Combine(Path.GetTempPath(), "momoka_sqlite_saves_" + Guid.NewGuid().ToString("N"));
        var dbPath = Path.Combine(root, "Demo Home.db");
        try
        {
            using (var store = new SqliteStore(dbPath))
                store.Save(residence);

            var listed = SqliteStore.ListSaves(root);
            var save = Assert.Single(listed);
            Assert.Equal("Demo Home", save.Name);
            Assert.Equal(UnitType.House, save.Type);
            Assert.Equal(residence.Bound, save.Bound);
            Assert.Empty(save.Entities); // metadata only — registry not loaded
        }
        finally
        {
            SqliteStore.Delete(dbPath);
        }
    }

    [Fact]
    public void Delete_RemovesDatabase()
    {
        var residence = DemoResidence();
        var dbPath = TempDb();
        using (var store = new SqliteStore(dbPath))
            store.Save(residence);
        Assert.True(File.Exists(dbPath));

        SqliteStore.Delete(dbPath);
        Assert.False(File.Exists(dbPath));
    }

    [Fact]
    public void Load_EmptyDatabase_ReturnsNull()
    {
        var dbPath = TempDb();
        try
        {
            using var store = new SqliteStore(dbPath);
            Assert.Null(store.Load());
        }
        finally
        {
            SqliteStore.Delete(dbPath);
        }
    }
}
