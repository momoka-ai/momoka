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
/// PlacementLayoutSource component; the region layer builds on demand.
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
        entity.AddProperties(new[] { new BooleanProperty(Property.IsImmutable, true) });
        return entity;
    }

    [Fact]
    public void Entities_ReflectsTheRootSpace()
    {
        var unit = new UnitLayout();
        var wall = Box("wall", 1, 3, 1);
        unit.PlaceAt(wall, new Float3(20, 0, 0));

        var registered = Assert.Single(unit.Entities);
        Assert.Equal(wall, registered);
    }

    [Fact]
    public void Surfaces_IncludesVoxelLayoutSourceEntities()
    {
        var unit = new UnitLayout();
        var source = new VoxelLayoutSourceEntity();
        unit.PlaceAt(source, new Float3(20, 0, 0));

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

    private sealed class FloorEntity : Entity
    {
        public FloorEntity()
        {
            Key = new Key("floor");
            Volume = new Box3D { SizeX = 5, SizeY = 1, SizeZ = 5 };
            this.AddProperties(new[] { new BooleanProperty(Property.IsImmutable, true) });
            var surface = new GridLayout<bool>(new Int2(5, 5), new Int3(0, 1, 0));
            surface.Fill(true, Int2.Zero, new Int2(5, 5));
            this.AddComponent(new PlacementLayoutSource { Layout = surface });
        }
    }

    [Fact]
    public void BuildLayout_BuildsAndQueries()
    {
        var unit = new UnitLayout();
        unit.PlaceAt(new FloorEntity(), new Float3(0, 0, 0));
        unit.PlaceAt(StructuralBox("wall", 1, 29, 5), new Float3(20, 10, 0)); // 中墙 x=2 全高

        var map = Region.BuildLayout(unit);
        Assert.NotNull(map.At(1, 5, 2));
        Assert.NotNull(map.At(3, 5, 2));
        Assert.NotEqual(map.At(1, 5, 2)!.Id, map.At(3, 5, 2)!.Id);
        Assert.Null(map.At(2, 5, 2)); // 中墙
    }

    // ── 实体放置（UnitLayout 接管原 VoxelLayout 的放置语义）──────

    [Fact]
    public void PlaceAt_WritesAllVoxels_AndRegisters()
    {
        var unit = new UnitLayout();
        var entity = Box("box", 2, 1, 2);

        Assert.True(unit.PlaceAt(entity, new Float3(50, 0, 50)));
        Assert.Equal(new Float3(50, 0, 50), entity.Pos.Absolute());

        // 全部 4 个体素格都写入（不只锚点）
        Assert.True(unit.Voxels[new Int3(5, 0, 5)] is not null);
        Assert.True(unit.Voxels[new Int3(6, 0, 5)] is not null);
        Assert.True(unit.Voxels[new Int3(5, 0, 6)] is not null);
        Assert.True(unit.Voxels[new Int3(6, 0, 6)] is not null);
        Assert.Same(entity, unit.FindEntity(entity.Id));
    }

    [Fact]
    public void IsEntityCollided_TrueWhenAnchorOccupied()
    {
        var unit = new UnitLayout();
        unit.PlaceAt(Box("box", 1, 1, 1), new Float3(50, 0, 50));

        var box = Box("box", 1, 1, 1);
        Assert.True(unit.IsCollidedVolume(new Position(new Float3(50, 0, 50)), box.Volume) is not null);
        Assert.False(unit.PlaceAt(box, new Float3(50, 0, 50)));
    }

    [Fact]
    public void IsEntityCollided_TrueWhenVoxelsOverlap_EvenIfAnchorsDiffer()
    {
        var unit = new UnitLayout();
        unit.PlaceAt(Box("box", 2, 1, 2), new Float3(50, 0, 50)); // 占用 (5..6, 5..6)

        // B 锚点 (6,0,5) 不同，但体素与 A 重叠
        var box = Box("box", 2, 1, 2);
        Assert.True(unit.IsCollidedVolume(new Position(new Float3(60, 0, 50)), box.Volume) is not null);
        Assert.False(unit.PlaceAt(box, new Float3(60, 0, 50)));
    }

    [Fact]
    public void IsEntityCollided_WithSpecificDest()
    {
        var unit = new UnitLayout();
        var dest = Box("box", 2, 1, 2);
        unit.PlaceAt(dest, new Float3(50, 0, 50));

        var src = Box("box", 1, 1, 1);
        Assert.True(unit.IsCollided(dest, src, new Float3(60, 0, 50))); // 命中 dest 体素
        Assert.False(unit.IsCollided(dest, src, new Float3(90, 0, 90))); // 不重叠
    }

    [Fact]
    public void PlaceAt_NextToEntity_Succeeds()
    {
        var unit = new UnitLayout();
        unit.PlaceAt(Box("box", 1, 1, 1), new Float3(50, 0, 50));

        Assert.True(unit.PlaceAt(Box("box", 1, 1, 1), new Float3(70, 0, 50)));
        Assert.Equal(2, unit.Entities.Count);
    }

    [Fact]
    public void DestroyAt_RemovesEntityCoveringAnyCell()
    {
        var unit = new UnitLayout();
        unit.PlaceAt(Box("box", 2, 1, 2), new Float3(50, 0, 50));

        // 锚点格
        Assert.True(unit.DestroyAt(new Int3(5, 0, 5)));
        Assert.True(unit.Voxels[new Int3(6, 0, 6)] is null);
        Assert.Empty(unit.Entities);

        // 非锚点格（按占用格索引）
        unit.PlaceAt(Box("box", 2, 1, 2), new Float3(50, 0, 50));
        Assert.True(unit.DestroyAt(new Int3(6, 0, 6)));
        Assert.Empty(unit.Entities);
        Assert.False(unit.DestroyAt(new Int3(5, 0, 5))); // 已移除
    }

    [Fact]
    public void Rebuild_RasterizesEntitiesBackIntoGrid()
    {
        var unit = new UnitLayout();
        var entity = Box("box", 2, 1, 2);
        unit.PlaceAt(entity, new Float3(50, 0, 50));

        // 直接低层写入一个游离引用（绕过同步）
        unit.Voxels[new Int3(0, 0, 0)] = entity;

        unit.Rebuild();

        Assert.True(unit.Voxels[new Int3(0, 0, 0)] is null); // 游离引用被清除
        Assert.True(unit.Voxels[new Int3(5, 0, 5)] is not null);
        Assert.True(unit.Voxels[new Int3(6, 0, 6)] is not null);
    }

    [Fact]
    public void GetEntitiesInBound_FindsEntitiesInBox()
    {
        var unit = new UnitLayout();
        var a = Box("box", 1, 1, 1);
        var b = Box("box", 1, 1, 1);
        unit.PlaceAt(a, new Float3(10, 0, 10));
        unit.PlaceAt(b, new Float3(80, 0, 80));

        var inBox = unit.GetEntitiesInBound(new Int2(0, 0), new Int2(3, 3));
        Assert.Equal(new[] { a }, inBox);
    }
}
