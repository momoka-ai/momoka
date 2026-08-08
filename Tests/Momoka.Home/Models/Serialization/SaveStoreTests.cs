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
/// SaveStore + SaveFactory round-trip a whole residence as a save folder:
/// Residence.json (identity + bound), Entities.json (entity snapshot),
/// Chunks/Layout.{x}.{z}.dat (voxel + region spans) and Regions.json (names).
/// Loading reconstructs the grid and region layer, and SaveFactory re-injects
/// them into a live Residence.
/// </summary>
public class SaveStoreTests
{
    private static Entity Box(string path, int sx, int sy, int sz) => new()
    {
        Key = new Key(path),
        Volume = new Box3D { SizeX = sx, SizeY = sy, SizeZ = sz },
    };

    private static Entity StructuralBox(string path, int sx, int sy, int sz)
    {
        var entity = Box(path, sx, sy, sz);
        entity.AddProperties(new[] { new BooleanProperty(BuiltinProperty.IsStructural, true) });
        return entity;
    }

    private static Entity SurfaceBox(string path, int sx, int sy, int sz, Int3 pos, int surfaceY)
    {
        var entity = StructuralBox(path, sx, sy, sz);
        var surface = new GridLayout<bool>(new Int2(sx, sz), new Int3(pos.X, surfaceY, pos.Z));
        surface.Fill(true, Int2.Zero, new Int2(sx, sz));
        entity.AddComponent(new PlacementLayoutSource { Layout = surface });
        return entity;
    }

    /// <summary>10×9×30 封闭空间，中墙 (x=5) 分左右两室。</summary>
    private static Residence DemoResidence()
    {
        var residence = new Residence { Name = "Demo Home", Address = "1 Sunshine Ave", Type = UnitType.House };
        var unit = residence.Layout;
        unit.PlaceAt(SurfaceBox("floor", 10, 1, 10, new Int3(0, 0, 0), 1), new Int3(0, 0, 0));
        unit.PlaceAt(StructuralBox("wall", 10, 29, 1), new Int3(0, 1, 0));
        unit.PlaceAt(StructuralBox("wall", 10, 29, 1), new Int3(0, 1, 9));
        unit.PlaceAt(StructuralBox("wall", 1, 29, 8), new Int3(0, 1, 1));
        unit.PlaceAt(StructuralBox("wall", 1, 29, 8), new Int3(9, 1, 1));
        unit.PlaceAt(StructuralBox("wall", 1, 29, 8), new Int3(5, 1, 1));
        unit.RebuildRegions();
        unit.Regions!.At(2, 5, 2)!.Name = "Bedroom";
        unit.Regions!.At(7, 5, 2)!.Name = "Study";
        return residence;
    }

    private static string TempRoot() =>
        Path.Combine(Path.GetTempPath(), "momoka_saves_" + Guid.NewGuid().ToString("N"));

    [Fact]
    public void SaveLoad_RoundTripsResidence()
    {
        var residence = DemoResidence();
        var root = TempRoot();
        try
        {
            SaveStore.Save(residence, root);

            var saveDir = Path.Combine(root, "Demo Home");
            Assert.True(File.Exists(Path.Combine(saveDir, SaveStore.ResidenceFile)));
            Assert.True(File.Exists(Path.Combine(saveDir, SaveStore.EntitiesFile)));
            Assert.True(File.Exists(Path.Combine(saveDir, SaveStore.RegionsFile)));
            Assert.True(File.Exists(Path.Combine(saveDir, "Chunks", "Layout.0.0.dat")));

            // 元数据列表（不加载网格）。
            var listed = SaveStore.ListSaves(root);
            var save = Assert.Single(listed);
            Assert.Equal("Demo Home", save.Name);
            Assert.Equal("1 Sunshine Ave", save.Address);
            Assert.Equal(UnitType.House, save.Type);
            Assert.Equal(residence.Layout.Layout.Bound, save.Bound);
            Assert.Null(save.Grid);

            // 完整加载。
            var loaded = SaveStore.Load(saveDir);
            Assert.Equal("Demo Home", loaded.Name);
            Assert.Equal(UnitType.House, loaded.Type);
            Assert.Equal(residence.Layout.Layout.Bound, loaded.Bound);
            Assert.Equal(residence.Entities.Count, loaded.Entities.Count);
            Assert.NotNull(loaded.Grid);
            Assert.NotNull(loaded.Regions);

            // 重建 Residence：实体、网格单元、区域与命名都应等价。
            var rebuilt = SaveFactory.BuildResidence(loaded);
            Assert.Equal("Demo Home", rebuilt.Name);
            Assert.Equal(residence.Entities.Count, rebuilt.Entities.Count);
            foreach (var entity in residence.Entities)
                Assert.NotNull(rebuilt.Layout.FindEntity(entity.Id));

            Assert.Equal(
                residence.Layout.Layout[new Int3(0, 1, 2)]!.Id,
                rebuilt.Layout.Layout[new Int3(0, 1, 2)]!.Id);

            var originalLeft = residence.Layout.Regions!.At(2, 5, 2);
            var rebuiltLeft = rebuilt.Layout.Regions!.At(2, 5, 2);
            Assert.NotNull(originalLeft);
            Assert.NotNull(rebuiltLeft);
            Assert.Equal(originalLeft!.Id, rebuiltLeft!.Id);
            Assert.Equal(originalLeft.Volume, rebuiltLeft.Volume);
            Assert.Equal("Bedroom", rebuiltLeft.Name);
            Assert.Equal("Study", rebuilt.Layout.Regions!.At(7, 5, 2)!.Name);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }
}
