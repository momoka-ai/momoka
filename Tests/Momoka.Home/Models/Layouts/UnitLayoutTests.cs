using Xunit;
using Momoka.Home;
using Momoka.Home.Components;
using Momoka.Home.Entities;
using Momoka.Home.Geometry;
using Momoka.Home.Layouts;
using Momoka.Home.Primitives;
namespace Momoka.Home.Tests.Models.Layouts;

/// <summary>
/// Checks the unit layout as the fully-3D spatial root: entities live in the
/// single root space; placement surfaces come from each entity's
/// VoxelLayoutSource component; the whole space composes upward via
/// IVoxelGeometry3D; the region layer builds on demand.
/// </summary>
public class UnitLayoutTests
{
    private sealed class VoxelLayoutSourceEntity : Entity
    {
        public VoxelLayoutSourceEntity()
        {
            Volume = new Box3D();
            AddComponent(new VoxelLayoutSource { Layouts = { new GridLayout<bool>(new Int2(2, 2)) } });
        }
    }

    private static Entity Box(string path, int sx, int sy, int sz) => new()
    {
        Key = new Key(path),
        Volume = new Box3D { SizeX = sx, SizeY = sy, SizeZ = sz },
    };

    [Fact]
    public void Entities_ReflectsTheRootSpace()
    {
        var unit = new UnitLayout();
        var wall = Box("wall", 1, 3, 1);
        unit.Layout.BuildAt(wall, new Int3(2, 0, 0));

        var registered = Assert.Single(unit.Entities);
        Assert.Equal(wall, registered);
    }

    [Fact]
    public void Surfaces_IncludesVoxelLayoutSourceEntities()
    {
        var unit = new UnitLayout();
        var source = new VoxelLayoutSourceEntity();
        unit.Layout.BuildAt(source, new Int3(2, 0, 0));

        var surface = source.GetComponent<VoxelLayoutSource>()!.Layouts.Single();
        Assert.Contains(surface, unit.Surfaces);
    }

    [Fact]
    public void Surfaces_Empty_WhenNothingDeclaresSurfaces()
    {
        var unit = new UnitLayout();
        Assert.Empty(unit.Surfaces);
    }

    [Fact]
    public void PlaceAt_And_DestroyAt_ComposeIntoAParent()
    {
        var unit = new UnitLayout();
        unit.Layout.BuildAt(Box("wall", 6, 1, 1), new Int3(2, 0, 0));

        var parent = new VoxelLayout<Entity>();
        unit.PlaceAt(parent, new Int3(0, 30, 0));
        Assert.True(parent.HasEntity(new Int3(2, 30, 0)));
        Assert.True(parent.HasEntity(new Int3(7, 30, 0)));

        unit.DestroyAt(parent, new Int3(0, 30, 0));
        Assert.False(parent.HasEntity(new Int3(2, 30, 0)));
    }

    [Fact]
    public void Cells3D_AreRootAbsolute()
    {
        var unit = new UnitLayout();
        unit.Layout.BuildAt(Box("wall", 6, 1, 1), new Int3(2, 0, 0));

        var cells = unit.Cells3D().ToList();
        Assert.Contains(new Int3(2, 0, 0), cells);
        Assert.Contains(new Int3(7, 0, 0), cells);
    }

    private sealed class FloorEntity : Entity
    {
        public FloorEntity()
        {
            Key = new Key("floor");
            Volume = new Box3D { SizeX = 5, SizeY = 1, SizeZ = 5 };
            var surface = new GridLayout<bool>(new Int2(5, 5), new Int3(0, 1, 0));
            surface.Fill(true, Int2.Zero, new Int2(5, 5));
            AddComponent(new VoxelLayoutSource { Layouts = { surface } });
        }
    }

    [Fact]
    public void RebuildRegions_BuildsAndQueries()
    {
        var unit = new UnitLayout();
        unit.Layout.BuildAt(new FloorEntity(), new Int3(0, 0, 0));
        unit.Layout.BuildAt(Box("wall", 1, 29, 5), new Int3(2, 1, 0)); // 中墙 x=2 全高

        var map = unit.RebuildRegions();
        Assert.Same(map, unit.Regions);
        Assert.NotNull(unit.RegionAt(new Int3(1, 5, 2)));
        Assert.NotNull(unit.RegionAt(new Int3(3, 5, 2)));
        Assert.NotEqual(unit.RegionAt(new Int3(1, 5, 2))!.Id, unit.RegionAt(new Int3(3, 5, 2))!.Id);
        Assert.Null(unit.RegionAt(new Int3(2, 5, 2))); // 中墙
    }
}
