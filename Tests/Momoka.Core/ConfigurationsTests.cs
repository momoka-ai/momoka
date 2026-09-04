using Xunit;
using Momoka.Core.Configurations;

namespace Momoka.Core.Tests;

/// <summary>Configurations：文件 / 二进制 / 数据库三种后端 + 统一值树、类型化读写与版本迁移链。</summary>
public sealed class ConfigurationsTests : IDisposable
{
    private readonly string _tempRoot;

    public ConfigurationsTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), "momoka-core-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempRoot);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_tempRoot, recursive: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // 清理尽力而为：临时目录删除的 IO 失败不影响测试结论
        }
    }

    [Fact]
    public void FileConfiguration_SaveAndLoad_RoundTripsValuesAndVersion()
    {
        string path = TempFile();

        var config = new FileConfiguration(path);
        config.Set("server.host", "localhost");
        config.Set("server.port", 8080);
        config.Set("server.ssl", true);
        config.Set("server.ratio", 1.5);
        config.Save();

        var loaded = new FileConfiguration(path);
        loaded.Load();

        Assert.Equal(new Version(1, 0), loaded.Version);
        Assert.Equal("localhost", loaded.Get<string>("server.host"));
        Assert.Equal(8080, loaded.Get<int>("server.port"));
        Assert.True(loaded.Get<bool>("server.ssl"));
        Assert.Equal(1.5, loaded.Get<double>("server.ratio"));
        Assert.Equal(new[] { "host", "port", "ratio", "ssl" }.OrderBy(x => x), loaded.GetKeys("server").OrderBy(x => x));
    }

    [Fact]
    public void FileConfiguration_UnknownFields_PreservedAcrossMigration()
    {
        string path = TempFile();

        var config = new FileConfiguration(path);
        config.Set("known.old", 1);
        config.Set("vendor.extra", "kept");
        config.Save();

        var migrated = new FileConfiguration(path, migrations: new[]
        {
            new Migration(new Version(1, 0), new Version(2, 0), c => c.Set("known.new", c.Get<int>("known.old"))),
        });
        migrated.Load();

        Assert.Equal(new Version(2, 0), migrated.Version);
        Assert.Equal(1, migrated.Get<int>("known.old"));
        Assert.Equal(1, migrated.Get<int>("known.new"));
        Assert.Equal("kept", migrated.Get<string>("vendor.extra"));
    }

    [Fact]
    public void FileConfiguration_MissingMigrationStep_Throws()
    {
        string path = TempFile();
        new FileConfiguration(path).Save();

        var config = new FileConfiguration(path, migrations: new[]
        {
            new Migration(new Version(1, 0), new Version(3, 0), _ => { }),
        }, targetVersion: new Version(4, 0));

        Assert.Throws<ConfigurationException>(() => config.Load());
    }

    [Fact]
    public void FileConfiguration_MissingFile_Throws()
    {
        var config = new FileConfiguration(Path.Combine(_tempRoot, "missing.toml"));
        Assert.Throws<FileNotFoundException>(() => config.Load());
    }

    [Fact]
    public void FileConfiguration_InvalidToml_Throws()
    {
        string path = TempFile();
        File.WriteAllText(path, "not = valid = toml");

        var config = new FileConfiguration(path);
        Assert.Throws<ConfigurationException>(() => config.Load());
    }

    [Fact]
    public void Configuration_Get_ConvertsEnumGuidAndDateTime()
    {
        string path = TempFile();
        var config = new FileConfiguration(path);
        config.Set("mode", Mode.Auto);
        config.Set("id", Guid.Parse("01234567-89ab-cdef-0123-456789abcdef"));
        var stamp = new DateTime(2024, 5, 6, 7, 8, 9, DateTimeKind.Utc);
        config.Set("stamp", stamp);
        config.Save();

        var loaded = new FileConfiguration(path);
        loaded.Load();

        Assert.Equal(Mode.Auto, loaded.Get<Mode>("mode"));
        Assert.Equal(Guid.Parse("01234567-89ab-cdef-0123-456789abcdef"), loaded.Get<Guid>("id"));
        Assert.Equal(stamp, loaded.Get<DateTime>("stamp"));
    }

    [Fact]
    public void Configuration_Get_MissingAndWrongType_Throws()
    {
        var config = new FileConfiguration(Path.Combine(_tempRoot, "x.toml"));
        config.Set("count", 3);
        config.Set("text", "abc");

        Assert.Throws<ConfigurationException>(() => config.Get<int>("absent"));
        Assert.Throws<ConfigurationException>(() => config.Get<int>("text"));
        Assert.False(config.TryGet<int>("absent", out _));
        Assert.True(config.TryGet<int>("count", out int value));
        Assert.Equal(3, value);
    }

    [Fact]
    public void Configuration_Set_SameTypeConversion_FailsFast()
    {
        var config = new FileConfiguration(Path.Combine(_tempRoot, "x.toml"));
        Assert.Throws<ConfigurationException>(() => config.Set("complex", new object()));
    }

    [Fact]
    public void BinaryConfiguration_RoundTripsValues()
    {
        var config = new BinaryConfiguration();
        config.Set("name", "alpha");
        config.Set("enabled", true);
        config.Set("count", 42L);
        config.Set("ratio", 2.5);
        config.Set("stamp", new DateTime(2024, 1, 2, 3, 4, 5));
        config.Set("tags", new[] { "a", "b" });
        config.Set("nested.x", 7);

        var loaded = BinaryConfiguration.FromBytes(config.ToBytes());

        Assert.Equal(new Version(1, 0), loaded.Version);
        Assert.Equal("alpha", loaded.Get<string>("name"));
        Assert.True(loaded.Get<bool>("enabled"));
        Assert.Equal(42L, loaded.Get<long>("count"));
        Assert.Equal(2.5, loaded.Get<double>("ratio"));
        Assert.Equal(new DateTime(2024, 1, 2, 3, 4, 5), loaded.Get<DateTime>("stamp"));
        Assert.Equal(new[] { "a", "b" }, loaded.GetValue("tags"));
        Assert.Equal(7, loaded.Get<int>("nested.x"));
    }

    [Fact]
    public void BinaryConfiguration_InvalidMagic_Throws()
    {
        Assert.Throws<ConfigurationException>(() => BinaryConfiguration.FromBytes(new byte[] { 1, 2, 3, 4 }));
    }

    [Fact]
    public void DatabaseConfiguration_SavesAndReloads()
    {
        var store = new MemoryStore();
        var config = new DatabaseConfiguration(store);
        config.Set("device.name", "lamp");
        config.Set("device.level", 5);
        config.Set("device.tags", new[] { "living", "rgb" });
        config.Save();

        var reloaded = new DatabaseConfiguration(store);

        Assert.Equal(new Version(1, 0), reloaded.Version);
        Assert.Equal("lamp", reloaded.Get<string>("device.name"));
        Assert.Equal(5, reloaded.Get<int>("device.level"));
        Assert.Equal(new[] { "living", "rgb" }, reloaded.GetValue("device.tags"));
        Assert.True(store.ReadAll().ContainsKey("device.name"));
        Assert.True(store.ReadAll().ContainsKey("version"));
    }

    [Fact]
    public void DatabaseConfiguration_Reload_PicksUpExternalWrites()
    {
        var store = new MemoryStore();
        var config = new DatabaseConfiguration(store);
        config.Set("server.port", 8080);
        config.Save();

        store.WriteAll(new Dictionary<string, string?> { ["server.port"] = "9090", ["version"] = "1.0" });
        config.Reload();

        Assert.Equal(9090, config.Get<int>("server.port"));
    }

    private string TempFile() => Path.Combine(_tempRoot, Guid.NewGuid().ToString("N") + ".toml");

    private enum Mode
    {
        Manual,
        Auto,
    }

    /// <summary>内存版配置存储（测试夹具）。</summary>
    private sealed class MemoryStore : IConfigurationStore
    {
        private Dictionary<string, string?> _rows = new(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, string?> ReadAll() =>
            _rows.ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.Ordinal);

        public void WriteAll(IReadOnlyDictionary<string, string?> values) =>
            _rows = values.ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.Ordinal);
    }
}
