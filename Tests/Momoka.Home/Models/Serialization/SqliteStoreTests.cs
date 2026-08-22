using Momoka.Home.Runtime;
using Xunit;
using Momoka.Home.Levels.Entities;
using Momoka.Home.Data.Sqlite;
using Momoka.Home.Levels.Volumes;
using Momoka.Home.Levels;
using Momoka.Home.Primitives;
using Momoka.Home.Levels.Entities.Components;
using Momoka.Home.Levels.Entities.Properties;
namespace Momoka.Home.Tests.Models.Serialization;

/// <summary>
/// SqliteStore round-trips the level data: a single-file database (one save per
/// server) holding the <c>Entities</c>, <c>Chunks</c> and <c>RegionNames</c>
/// tables. The store holds one open connection for its lifetime; every
/// operation goes through linq2db's functional API.
/// </summary>
public class SqliteStoreTests
{
    private static Entity Box(string path, int sx, int sy, int sz) => new()
    {
        Key = new Key(path),
        Volume = new Box { SizeX = sx, SizeY = sy, SizeZ = sz },
    };

    /// <summary>服务器形态的 LevelData：隐藏 Home 实体（Type 持久化真相）+ 注册实体 + 放置实体。</summary>
    private static ServerLevelData DemoLevel()
    {
        var server = new ServerLevelData(); // 构造自动创建 Home 实体
        server.Type = LevelType.House;

        var lamp = Box("lamp", 1, 1, 1);
        lamp.Transform = new Transform(new Float3(20, 10, 30), Rotation.Identity);
        lamp.AddProperties(new[] { new BooleanProperty(Property.IsImmutable, true) });

        var ac = Box("ac", 2, 1, 1);
        ac.Transform = new Transform(new Float3(40, 20, 50), Rotation.Identity);
        ac.AddComponent(new CommandTarget { Commands = "[\"turn_on\",\"turn_off\"]" });

        server.Entities.AddRange(new[] { lamp, ac });
        server.Layout.Add(lamp, new Position(new Float3(20, 10, 30))); // 放置 → 体素占格
        return server;
    }

    private static string TempDb() =>
        Path.Combine(Path.GetTempPath(), "momoka_sqlite_" + Guid.NewGuid().ToString("N") + ".db");

    [Fact]
    public void SaveLoad_RoundTripsEntitiesTypeAndVoxels()
    {
        var level = DemoLevel();
        var dbPath = TempDb();
        try
        {
            using (var store = new SqliteStore(dbPath))
            {
                store.Save(level);
                Assert.True(File.Exists(dbPath));

                var loaded = store.Load();
                Assert.NotNull(loaded);
                Assert.Equal(LevelType.House, loaded!.Type); // 从 Home 实体 unit_type 还原
                Assert.Equal(level.Entities.Count, loaded.Entities.Count);
                Assert.Single(loaded.Entities, e => e.Key == LevelData.HomeKey); // 隐藏 Home 实体

                // 注册实体逐字段核对
                foreach (var original in level.Entities.Where(e => e.Key != LevelData.HomeKey))
                {
                    var rebuilt = Assert.Single(loaded.Entities, e => e.Id == original.Id);
                    Assert.Equal(original.Key, rebuilt.Key);
                    Assert.Equal(original.Transform, rebuilt.Transform);
                    Assert.Equal(original.Volume.GetType(), rebuilt.Volume.GetType());
                    Assert.Equal(original.Properties.Count, rebuilt.Properties.Count);
                    Assert.Equal(original.Components.Count, rebuilt.Components.Count);
                }

                // 体素往返：放置实体在网格中（palette 按 Id 解析）
                var lamp = level.Entities.First(e => e.Key == new Key("lamp"));
                var placed = loaded.Layout.Voxels[new Int3(2, 1, 3)];
                Assert.NotNull(placed);
                Assert.Equal(lamp.Id, placed.Id);
            }
        }
        finally
        {
            SqliteStore.Delete(dbPath);
        }
    }

    [Fact]
    public void Save_Twice_KeepsSingleRows()
    {
        var level = DemoLevel();
        var dbPath = TempDb();
        try
        {
            using (var store = new SqliteStore(dbPath))
            {
                store.Save(level);
                store.Save(level);

                var loaded = store.Load();
                Assert.NotNull(loaded);
                Assert.Equal(level.Entities.Count, loaded!.Entities.Count); // 不重复
                Assert.Equal(level.Type, loaded.Type);
            }
        }
        finally
        {
            SqliteStore.Delete(dbPath);
        }
    }

    [Fact]
    public void Delete_RemovesDatabase()
    {
        var level = DemoLevel();
        var dbPath = TempDb();
        using (var store = new SqliteStore(dbPath))
            store.Save(level);
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
