using Xunit;
using Momoka.Home;
using Momoka.Home.Components;
using Momoka.Home.Entities;
using Momoka.Home.Geometry;
using Momoka.Home.Layouts;
using Momoka.Home.Primitives;
using Momoka.Home.Properties;
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
            this.AddComponent(new PlacementLayoutSource { Layout = new GridLayout<bool>(new Int2(2, 2)) });
        }
    }

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

    [Fact]
    public void Entities_ReflectsTheRootSpace()
    {
        var unit = new UnitLayout();
        var wall = Box("wall", 1, 3, 1);
        unit.BuildAt(wall, new Int3(2, 0, 0));

        var registered = Assert.Single(unit.Entities);
        Assert.Equal(wall, registered);
    }

    [Fact]
    public void Surfaces_IncludesVoxelLayoutSourceEntities()
    {
        var unit = new UnitLayout();
        var source = new VoxelLayoutSourceEntity();
        unit.BuildAt(source, new Int3(2, 0, 0));

        var surface = source.GetComponent<PlacementLayoutSource>()!.Layout;
        Assert.NotNull(surface);
        Assert.Contains(surface!, unit.Surfaces);
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
        unit.BuildAt(Box("wall", 6, 1, 1), new Int3(2, 0, 0));

        var parent = new VoxelLayout<Entity>();
        unit.PlaceAt(parent, new Int3(0, 30, 0));
        Assert.True(parent[new Int3(2, 30, 0)] is not null);
        Assert.True(parent[new Int3(7, 30, 0)] is not null);

        unit.DestroyAt(parent, new Int3(0, 30, 0));
        Assert.True(parent[new Int3(2, 30, 0)] is null);
    }

    [Fact]
    public void Cells3D_AreRootAbsolute()
    {
        var unit = new UnitLayout();
        unit.BuildAt(Box("wall", 6, 1, 1), new Int3(2, 0, 0));

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
            this.AddProperties(new[] { new BooleanProperty(BuiltinProperty.IsStructural, true) });
            var surface = new GridLayout<bool>(new Int2(5, 5), new Int3(0, 1, 0));
            surface.Fill(true, Int2.Zero, new Int2(5, 5));
            this.AddComponent(new PlacementLayoutSource { Layout = surface });
        }
    }

    [Fact]
    public void RebuildRegions_BuildsAndQueries()
    {
        var unit = new UnitLayout();
        unit.BuildAt(new FloorEntity(), new Int3(0, 0, 0));
        unit.BuildAt(StructuralBox("wall", 1, 29, 5), new Int3(2, 1, 0)); // 中墙 x=2 全高

        var map = unit.RebuildRegions();
        Assert.Same(map, unit.Regions);
        Assert.NotNull(unit.RegionAt(new Int3(1, 5, 2)));
        Assert.NotNull(unit.RegionAt(new Int3(3, 5, 2)));
        Assert.NotEqual(unit.RegionAt(new Int3(1, 5, 2))!.Id, unit.RegionAt(new Int3(3, 5, 2))!.Id);
        Assert.Null(unit.RegionAt(new Int3(2, 5, 2))); // 中墙
    }

    // ── 实体放置（UnitLayout 接管原 VoxelLayout 的放置语义）──────

    [Fact]
    public void BuildAt_WritesAllVoxels_AndRegisters()
    {
        var unit = new UnitLayout();
        var entity = Box("box", 2, 1, 2);

        Assert.True(unit.BuildAt(entity, new Int3(5, 0, 5)));
        Assert.Equal(new Int3(5, 0, 5), entity.Coords);

        // 全部 4 个体素格都写入（不只锚点）
        Assert.True(unit.Layout[new Int3(5, 0, 5)] is not null);
        Assert.True(unit.Layout[new Int3(6, 0, 5)] is not null);
        Assert.True(unit.Layout[new Int3(5, 0, 6)] is not null);
        Assert.True(unit.Layout[new Int3(6, 0, 6)] is not null);
        Assert.Same(entity, unit.FindEntity(entity.Id));
    }

    [Fact]
    public void IsEntityCollided_TrueWhenAnchorOccupied()
    {
        var unit = new UnitLayout();
        unit.BuildAt(Box("box", 1, 1, 1), new Int3(5, 0, 5));

        Assert.True(unit.IsEntityCollided(Box("box", 1, 1, 1), new Int3(5, 0, 5)));
        Assert.False(unit.BuildAt(Box("box", 1, 1, 1), new Int3(5, 0, 5)));
    }

    [Fact]
    public void IsEntityCollided_TrueWhenVoxelsOverlap_EvenIfAnchorsDiffer()
    {
        var unit = new UnitLayout();
        unit.BuildAt(Box("box", 2, 1, 2), new Int3(5, 0, 5)); // 占用 (5..6, 5..6)

        // B 锚点 (6,0,5) 不同，但体素与 A 重叠
        Assert.True(unit.IsEntityCollided(Box("box", 2, 1, 2), new Int3(6, 0, 5)));
        Assert.False(unit.BuildAt(Box("box", 2, 1, 2), new Int3(6, 0, 5)));
    }

    [Fact]
    public void IsEntityCollided_WithSpecificDest()
    {
        var unit = new UnitLayout();
        var dest = Box("box", 2, 1, 2);
        unit.BuildAt(dest, new Int3(5, 0, 5));

        var src = Box("box", 1, 1, 1);
        Assert.True(unit.IsEntityCollided(dest, src, new Int3(6, 0, 5))); // 命中 dest 体素
        Assert.False(unit.IsEntityCollided(dest, src, new Int3(9, 0, 9))); // 不重叠
    }

    [Fact]
    public void BuildAt_NextToEntity_Succeeds()
    {
        var unit = new UnitLayout();
        unit.BuildAt(Box("box", 1, 1, 1), new Int3(5, 0, 5));

        Assert.True(unit.BuildAt(Box("box", 1, 1, 1), new Int3(7, 0, 5)));
        Assert.Equal(2, unit.Entities.Count);
    }

    [Fact]
    public void DestroyAt_RemovesEntityByRegisteredPosition()
    {
        var unit = new UnitLayout();
        unit.BuildAt(Box("box", 2, 1, 2), new Int3(5, 0, 5));

        Assert.True(unit.DestroyAt(new Int3(5, 0, 5)));
        Assert.True(unit.Layout[new Int3(6, 0, 6)] is null);
        Assert.Empty(unit.Entities);
        Assert.False(unit.DestroyAt(new Int3(5, 0, 5))); // 已移除
    }

    [Fact]
    public void DestroyTarget_RemovesEntityCoveringAnyCell()
    {
        var unit = new UnitLayout();
        unit.BuildAt(Box("box", 2, 1, 2), new Int3(5, 0, 5));

        Assert.True(unit.DestroyTarget(new Int3(6, 0, 6))); // 非锚点格
        Assert.Empty(unit.Entities);
        Assert.True(unit.Layout[new Int3(5, 0, 5)] is null);
    }

    [Fact]
    public void Rebuild_RasterizesEntitiesBackIntoGrid()
    {
        var unit = new UnitLayout();
        var entity = Box("box", 2, 1, 2);
        unit.BuildAt(entity, new Int3(5, 0, 5));

        // 直接低层写入一个游离引用（绕过同步）
        unit.Layout[new Int3(0, 0, 0)] = entity;

        unit.Rebuild();

        Assert.True(unit.Layout[new Int3(0, 0, 0)] is null); // 游离引用被清除
        Assert.True(unit.Layout[new Int3(5, 0, 5)] is not null);
        Assert.True(unit.Layout[new Int3(6, 0, 6)] is not null);
    }

    [Fact]
    public void GetEntitiesInBound_And_OfType_Filter()
    {
        var unit = new UnitLayout();
        var a = Box("box", 1, 1, 1);
        var b = Box("box", 1, 1, 1);
        unit.BuildAt(a, new Int3(1, 0, 1));
        unit.BuildAt(b, new Int3(8, 0, 8));

        var inBox = unit.GetEntitiesInBound(new Int2(0, 0), new Int2(3, 3));
        Assert.Equal(new[] { a }, inBox);

        var ofType = unit.GetEntitiesOfType<Entity>();
        Assert.Equal(2, ofType.Count);
    }
}
