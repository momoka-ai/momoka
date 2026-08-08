using Xunit;
using Momoka.Home.Components;
using Momoka.Home.Entities;
using Momoka.Home.Geometry;
using Momoka.Home.Layouts;
using Momoka.Home.Primitives;
using Momoka.Home.Properties;
using Momoka.Home.Storage;
namespace Momoka.Home.Tests.Models.Serialization;

/// <summary>
/// LayoutChunkCodec persists the voxel layer as per-chunk binary files:
/// paletted sections (palette of entity ids + packed words) round-trip cell
/// references exactly — including multi-section, multi-chunk and
/// negative-coordinate chunks.
/// </summary>
public class VoxelLayoutChunkCodecTests
{
    private static Entity Box(string path, int sx, int sy, int sz) => new()
    {
        Key = new Key(path),
        Volume = new Box3D { SizeX = sx, SizeY = sy, SizeZ = sz },
    };

    private static string TempDir() =>
        Path.Combine(Path.GetTempPath(), "momoka_chunks_" + Guid.NewGuid().ToString("N"));

    /// <summary>
    /// 一个跨多区块、多 section、含负坐标的场景：
    /// wall 占 chunk(0,0) 的 section 0 和 2；floor 占 chunk(1,1)；pillar 占 chunk(-1,-1)。
    /// </summary>
    private static (VoxelLayout<Entity> Layout, List<Entity> Entities) Scene()
    {
        var layout = new VoxelLayout<Entity>
        {
            Bound = Bound.FromCorners(Int3.Zero, new Int3(40, 45, 40)),
        };
        var entities = new List<Entity>();

        var wall = Box("wall", 1, 30, 1);
        entities.Add(wall);
        layout[new Int3(0, 0, 0)] = wall;
        layout[new Int3(0, 1, 0)] = wall;
        layout[new Int3(0, 32, 0)] = wall; // section 2

        var floor = Box("floor", 2, 1, 2);
        entities.Add(floor);
        layout[new Int3(17, 0, 17)] = floor;
        layout[new Int3(18, 0, 17)] = floor;
        layout[new Int3(17, 0, 18)] = floor;
        layout[new Int3(18, 0, 18)] = floor;

        var pillar = Box("pillar", 1, 1, 1);
        entities.Add(pillar);
        layout[new Int3(-1, 0, -1)] = pillar;

        return (layout, entities);
    }

    [Fact]
    public void SaveLoad_RoundTripsCellsAndEntities()
    {
        var (scene, entities) = Scene();
        var dir = TempDir();
        try
        {
            LayoutChunkCodec.Save(scene, null, dir);

            Assert.True(File.Exists(Path.Combine(dir, "Layout.0.0.dat")));
            Assert.True(File.Exists(Path.Combine(dir, "Layout.1.1.dat")));
            Assert.True(File.Exists(Path.Combine(dir, "Layout.-1.-1.dat")));

            var loaded = LayoutChunkCodec.Load(dir, entities).Grid;

            Assert.Same(entities[0], loaded[new Int3(0, 0, 0)]);   // wall
            Assert.Same(entities[0], loaded[new Int3(0, 1, 0)]);
            Assert.Same(entities[0], loaded[new Int3(0, 32, 0)]);  // section 2 还原
            Assert.Same(entities[1], loaded[new Int3(17, 0, 17)]); // floor
            Assert.Same(entities[1], loaded[new Int3(18, 0, 18)]);
            Assert.Same(entities[2], loaded[new Int3(-1, 0, -1)]); // pillar（负坐标 chunk）
            Assert.Null(loaded[new Int3(0, 2, 0)]);                // wall 上方空洞
            Assert.Null(loaded[new Int3(5, 5, 5)]);                // 空区域
        }
        finally
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void Save_RemovesStaleChunkFiles()
    {
        var (scene, entities) = Scene();
        var dir = TempDir();
        try
        {
            LayoutChunkCodec.Save(scene, null, dir);
            Assert.True(File.Exists(Path.Combine(dir, "Layout.1.1.dat")));

            // 清空 chunk(1,1) 的唯一数据：floor 的 4 个格
            scene[new Int3(17, 0, 17)] = default!;
            scene[new Int3(18, 0, 17)] = default!;
            scene[new Int3(17, 0, 18)] = default!;
            scene[new Int3(18, 0, 18)] = default!;

            LayoutChunkCodec.Save(scene, null, dir);

            Assert.False(File.Exists(Path.Combine(dir, "Layout.1.1.dat"))); // 空 chunk 文件被清理
            Assert.True(File.Exists(Path.Combine(dir, "Layout.0.0.dat")));
            Assert.True(File.Exists(Path.Combine(dir, "Layout.-1.-1.dat")));
        }
        finally
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void Load_MissingDirectory_ReturnsEmptyLayout()
    {
        var loaded = LayoutChunkCodec.Load(TempDir(), Array.Empty<Entity>()).Grid;
        Assert.Null(loaded[new Int3(0, 0, 0)]);
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
        var (scene, _) = Scene();
        var dir = TempDir();
        try
        {
            LayoutChunkCodec.Save(scene, null, dir);
            var bytes = File.ReadAllBytes(Path.Combine(dir, "Layout.0.0.dat"));

            Assert.Throws<InvalidDataException>(() =>
                LayoutChunkCodec.Decode(new Int2(0, 0), bytes, new Dictionary<Guid, Entity>()));
        }
        finally
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
        }
    }

    /// <summary>结构件盒子：is_structural 标记（墙 / 天花板 / 门）。</summary>
    private static Entity StructuralBox(string path, int sx, int sy, int sz)
    {
        var entity = Box(path, sx, sy, sz);
        entity.AddProperties(new[] { new BooleanProperty(BuiltinProperty.IsStructural, true) });
        return entity;
    }

    /// <summary>结构件盒子：顶面放置面（Up，Offset 在 surfaceY）+ is_structural。</summary>
    private static Entity SurfaceBox(string path, int sx, int sy, int sz, Int3 pos, int surfaceY)
    {
        var entity = StructuralBox(path, sx, sy, sz);
        var surface = new GridLayout<bool>(new Int2(sx, sz), new Int3(pos.X, surfaceY, pos.Z));
        surface.Fill(true, Int2.Zero, new Int2(sx, sz));
        entity.AddComponent(new PlacementLayoutSource { Layout = surface });
        return entity;
    }

    /// <summary>10×9×30 封闭空间，中墙 (x=5) 分左右两室：左室 x=1..4、右室 x=6..8。</summary>
    private static UnitLayout TwoRoomScene()
    {
        var l = new UnitLayout();
        l.PlaceAt(SurfaceBox("floor", 10, 1, 10, new Int3(0, 0, 0), 1), new Int3(0, 0, 0));
        l.PlaceAt(StructuralBox("ceiling", 10, 1, 10), new Int3(0, 30, 0));
        l.PlaceAt(StructuralBox("wall", 10, 29, 1), new Int3(0, 1, 0));
        l.PlaceAt(StructuralBox("wall", 10, 29, 1), new Int3(0, 1, 9));
        l.PlaceAt(StructuralBox("wall", 1, 29, 8), new Int3(0, 1, 1));
        l.PlaceAt(StructuralBox("wall", 1, 29, 8), new Int3(9, 1, 1));
        l.PlaceAt(StructuralBox("wall", 1, 29, 8), new Int3(5, 1, 1));
        return l;
    }

    [Fact]
    public void SaveLoad_RoundTripsRegionLayer()
    {
        var unit = TwoRoomScene();
        var regions = Region.BuildLayout(unit);
        var dir = TempDir();
        try
        {
            LayoutChunkCodec.Save(unit.Layout, regions, dir);
            var loaded = LayoutChunkCodec.Load(dir, unit.Entities);
            Assert.NotNull(loaded.Grid);

            var regionsFile = Path.Combine(dir, "Regions.json");
            RegionsCodec.Save(regions, regionsFile);
            var restored = RegionsCodec.Load(loaded.RegionColumns, regionsFile);

            var left = restored.At(2, 5, 2);
            var right = restored.At(7, 5, 2);
            Assert.NotNull(left);
            Assert.NotNull(right);
            Assert.NotEqual(left!.Id, right!.Id);
            Assert.Equal(regions.At(2, 5, 2)!.Id, left.Id);
            Assert.Equal(regions.At(7, 5, 2)!.Id, right.Id);
            Assert.Equal(regions.At(2, 5, 2)!.Volume, left.Volume);
            Assert.Equal(regions.At(2, 5, 2)!.Area, left.Area);
        }
        finally
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
        }
    }
}
