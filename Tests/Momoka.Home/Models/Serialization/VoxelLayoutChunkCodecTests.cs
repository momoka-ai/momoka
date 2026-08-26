using Xunit;
using Momoka.Home.Levels.Entities;
using Momoka.Home.Levels.Volumes;
using Momoka.Home.Levels.Layouts;
using Momoka.Home.Primitives;
using Momoka.Home.Data;
using Momoka.Home.Data.Sqlite;
using Momoka.Home.Levels;
using Momoka.Home.Levels.Entities.Components;
using Momoka.Home.Levels.Entities.Properties;
namespace Momoka.Home.Tests.Models.Serialization;

/// <summary>
/// LayoutChunkCodec persists the voxel layer as per-chunk binary payloads
/// (stored in the SQLite <c>Chunks</c> table): paletted sections (palette of
/// entity ids + packed words) round-trip cell references exactly — including
/// multi-section, multi-chunk and negative-coordinate chunks.
/// </summary>
public class VoxelLayoutChunkCodecTests
{
    private static Entity Box(string path, int sx, int sy, int sz) => new()
    {
        Key = new Key(path),
        Volume = new Box { SizeX = sx, SizeY = sy, SizeZ = sz },
    };

    private static string TempDb() =>
        Path.Combine(Path.GetTempPath(), "momoka_chunks_" + Guid.NewGuid().ToString("N") + ".db");

    /// <summary>
    /// 一个跨多区块、多 section、含负坐标的场景：
    /// wall 占 chunk(0,0) 的 section 0 和 2；floor 占 chunk(1,1)；pillar 占 chunk(-1,-1)。
    /// </summary>
    private static LevelData Scene()
    {
        var data = new LevelData
        {
            Type = LevelType.House,
        };
        data.Layout.Voxels.Bound = Bound.FromCorners(Int3.Zero.ToFloat3(), new Int3(40, 45, 40).ToFloat3());

        var wall = Box("wall", 1, 30, 1);
        data.Entities.Add(wall);
        data.Layout.Voxels[new Int3(0, 0, 0)] = wall;
        data.Layout.Voxels[new Int3(0, 1, 0)] = wall;
        data.Layout.Voxels[new Int3(0, 32, 0)] = wall; // section 2

        var floor = Box("floor", 2, 1, 2);
        data.Entities.Add(floor);
        data.Layout.Voxels[new Int3(17, 0, 17)] = floor;
        data.Layout.Voxels[new Int3(18, 0, 17)] = floor;
        data.Layout.Voxels[new Int3(17, 0, 18)] = floor;
        data.Layout.Voxels[new Int3(18, 0, 18)] = floor;

        var pillar = Box("pillar", 1, 1, 1);
        data.Entities.Add(pillar);
        data.Layout.Voxels[new Int3(-1, 0, -1)] = pillar;

        return data;
    }

    [Fact]
    public void SaveLoad_RoundTripsCellsAndEntities()
    {
        var scene = Scene();
        var dbPath = TempDb();
        try
        {
            using var store = new SqliteStore(dbPath);
            store.Save(scene);

            var loaded = store.Load()!;
            var wall = loaded.Entities.First(e => e.Key == new Key("wall"));
            var floor = loaded.Entities.First(e => e.Key == new Key("floor"));
            var pillar = loaded.Entities.First(e => e.Key == new Key("pillar"));

            Assert.Same(wall, loaded.Layout.Voxels[new Int3(0, 0, 0)]);   // wall
            Assert.Same(wall, loaded.Layout.Voxels[new Int3(0, 1, 0)]);
            Assert.Same(wall, loaded.Layout.Voxels[new Int3(0, 32, 0)]);  // section 2 还原
            Assert.Same(floor, loaded.Layout.Voxels[new Int3(17, 0, 17)]); // floor
            Assert.Same(floor, loaded.Layout.Voxels[new Int3(18, 0, 18)]);
            Assert.Same(pillar, loaded.Layout.Voxels[new Int3(-1, 0, -1)]); // pillar（负坐标 chunk）
            Assert.Null(loaded.Layout.Voxels[new Int3(0, 2, 0)]);                       // wall 上方空洞
            Assert.Null(loaded.Layout.Voxels[new Int3(5, 5, 5)]);                       // 空区域
        }
        finally
        {
            SqliteStore.Delete(dbPath);
        }
    }

    [Fact]
    public void Save_ReplacesWholesale_EmptyChunksVanish()
    {
        var scene = Scene();
        var dbPath = TempDb();
        try
        {
            using var store = new SqliteStore(dbPath);
            store.Save(scene);

            // 清空 chunk(1,1) 的唯一数据：floor 的 4 个格
            scene.Layout.Voxels[new Int3(17, 0, 17)] = default!;
            scene.Layout.Voxels[new Int3(18, 0, 17)] = default!;
            scene.Layout.Voxels[new Int3(17, 0, 18)] = default!;
            scene.Layout.Voxels[new Int3(18, 0, 18)] = default!;
            store.Save(scene); // 全量替换：空 chunk 不再写行

            var loaded = store.Load()!;
            var wall = loaded.Entities.First(e => e.Key == new Key("wall"));
            var pillar = loaded.Entities.First(e => e.Key == new Key("pillar"));
            Assert.Null(loaded.Layout.Voxels[new Int3(17, 0, 17)]); // 空 chunk 消失
            Assert.Same(wall, loaded.Layout.Voxels[new Int3(0, 0, 0)]);   // wall 保留
            Assert.Same(pillar, loaded.Layout.Voxels[new Int3(-1, 0, -1)]); // pillar 保留
        }
        finally
        {
            SqliteStore.Delete(dbPath);
        }
    }

    [Fact]
    public void Decode_CorruptData_Throws()
    {
        Assert.Throws<InvalidDataException>(() =>
            LayoutChunkCodec.Decode(Int2.Zero, new byte[] { 1, 2, 3, 4, 5 }, new Dictionary<Guid, Entity>()));
    }

    [Fact]
    public void Decode_UnknownPaletteEntity_Throws()
    {
        var scene = Scene();
        var dbPath = TempDb();
        try
        {
            using var store = new SqliteStore(dbPath);
            store.Save(scene);
            var loaded = store.Load()!;
            var chunk = loaded.Layout.Voxels.Chunks.First(c => c.Index.X == 0 && c.Index.Z == 0);
            var bytes = LayoutChunkCodec.Encode(chunk);

            // 用未知实体表解码 → palette 解析失败
            Assert.Throws<InvalidDataException>(() =>
                LayoutChunkCodec.Decode(new Int2(0, 0), bytes, new Dictionary<Guid, Entity>()));
        }
        finally
        {
            SqliteStore.Delete(dbPath);
        }
    }
}
