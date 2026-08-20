using Xunit;
using Momoka.Home;
using Momoka.Home.Entities;
using Momoka.Home.Geometry;
using Momoka.Home.Layouts;
using Momoka.Home.Primitives;
using Momoka.Home.Entities.Components;
using Momoka.Home.Entities.Properties;
namespace Momoka.Home.Tests.Models.Layouts;

/// <summary>
/// Checks the unit layout as the fully-3D spatial root: entities live in the
/// single root space; placement surfaces come from each entity's
/// PlacementLayoutSource component; the region layer builds on demand.
/// </summary>
public class UnitLayoutTests
{
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

    private static PlacementLayoutSource Surface(Int2 size)
    {
        var grid = new GridLayout<bool>(size);
        grid.Fill(true, Int2.Zero, size);
        return new PlacementLayoutSource { Layout = grid };
    }

    [Fact]
    public void Entities_ReflectsTheRootSpace()
    {
        var unit = new UnitLayout();
        var wall = Box("wall", 1, 3, 1);
        unit.Add(wall, new Position(new Float3(20, 0, 0)));

        var registered = Assert.Single(unit.Entities);
        Assert.Equal(wall, registered);
    }

    private sealed class FloorEntity : Entity
    {
        public FloorEntity()
        {
            Key = new Key("floor");
            Volume = new Box3D { SizeX = 5, SizeY = 1, SizeZ = 5 };
            this.AddProperties(new[] { new BooleanProperty(Property.IsImmutable, true) });
            var surface = new GridLayout<bool>(new Int2(5, 5));
            surface.Fill(true, Int2.Zero, new Int2(5, 5));
            this.AddComponent(new PlacementLayoutSource
            {
                Layout = surface,
                Transform = new Transform(new Float3(0, 10, 0), Rotation.Up),
            });
        }
    }

    [Fact]
    public void BuildLayout_BuildsAndQueries()
    {
        var unit = new UnitLayout();
        unit.Add(new FloorEntity(), new Position(new Float3(0, 0, 0)));
        unit.Add(StructuralBox("wall", 1, 29, 5), new Position(new Float3(20, 10, 0))); // 中墙 x=2 全高

        var map = Region.BuildLayout(unit);
        Assert.NotNull(map.At(1, 5, 2));
        Assert.NotNull(map.At(3, 5, 2));
        Assert.NotEqual(map.At(1, 5, 2)!.Id, map.At(3, 5, 2)!.Id);
        Assert.Null(map.At(2, 5, 2)); // 中墙
    }

    // ── 实体放置（UnitLayout 接管原 VoxelLayout 的放置语义）──────

    [Fact]
    public void Add_WritesAllVoxels_AndRegisters()
    {
        var unit = new UnitLayout();
        var entity = Box("box", 2, 1, 2);

        Assert.True(unit.Add(entity, new Position(new Float3(50, 0, 50))));
        Assert.Equal(new Float3(50, 0, 50), entity.Transform.Position);

        // 全部 4 个体素格都写入（不只锚点）
        Assert.True(unit.Voxels[new Int3(5, 0, 5)] is not null);
        Assert.True(unit.Voxels[new Int3(6, 0, 5)] is not null);
        Assert.True(unit.Voxels[new Int3(5, 0, 6)] is not null);
        Assert.True(unit.Voxels[new Int3(6, 0, 6)] is not null);
        Assert.Same(entity, unit.Find(entity.Id));
    }

    [Fact]
    public void IsEntityCollided_TrueWhenAnchorOccupied()
    {
        var unit = new UnitLayout();
        unit.Add(Box("box", 1, 1, 1), new Position(new Float3(50, 0, 50)));

        var box = Box("box", 1, 1, 1);
        Assert.True(unit.IsCollidedVolume(new Position(new Float3(50, 0, 50)), box.Volume) is not null);
        Assert.False(unit.Add(box, new Position(new Float3(50, 0, 50))));
    }

    [Fact]
    public void IsEntityCollided_TrueWhenVoxelsOverlap_EvenIfAnchorsDiffer()
    {
        var unit = new UnitLayout();
        unit.Add(Box("box", 2, 1, 2), new Position(new Float3(50, 0, 50))); // 占用 (5..6, 5..6)

        // B 锚点 (6,0,5) 不同，但体素与 A 重叠
        var box = Box("box", 2, 1, 2);
        Assert.True(unit.IsCollidedVolume(new Position(new Float3(60, 0, 50)), box.Volume) is not null);
        Assert.False(unit.Add(box, new Position(new Float3(60, 0, 50))));
    }

    [Fact]
    public void IsEntityCollided_WithSpecificDest()
    {
        var unit = new UnitLayout();
        var dest = Box("box", 2, 1, 2);
        unit.Add(dest, new Position(new Float3(50, 0, 50)));

        var src = Box("box", 1, 1, 1);
        Assert.True(unit.IsCollided(dest, src, new Float3(60, 0, 50))); // 命中 dest 体素
        Assert.False(unit.IsCollided(dest, src, new Float3(90, 0, 90))); // 不重叠
    }

    [Fact]
    public void Add_NextToEntity_Succeeds()
    {
        var unit = new UnitLayout();
        unit.Add(Box("box", 1, 1, 1), new Position(new Float3(50, 0, 50)));

        Assert.True(unit.Add(Box("box", 1, 1, 1), new Position(new Float3(70, 0, 50))));
        Assert.Equal(2, unit.Entities.Count);
    }

    [Fact]
    public void Remove_RemovesEntityCoveringAnyCell()
    {
        var unit = new UnitLayout();
        unit.Add(Box("box", 2, 1, 2), new Position(new Float3(50, 0, 50)));

        // 锚点格
        Assert.True(unit.Remove(new Position(new Float3(50, 0, 50))) is not null);
        Assert.True(unit.Voxels[new Int3(6, 0, 6)] is null);
        Assert.Empty(unit.Entities);

        // 非锚点格（按占用格索引）
        unit.Add(Box("box", 2, 1, 2), new Position(new Float3(50, 0, 50)));
        Assert.True(unit.Remove(new Position(new Float3(60, 0, 60))) is not null);
        Assert.Empty(unit.Entities);
        Assert.Null(unit.Remove(new Position(new Float3(50, 0, 50)))); // 已移除
    }

    // ── 自动寻位（Add(Entity)）──────────────────────────

    [Fact]
    public void Add_AutoPlaces_OnGroundWhenEmpty()
    {
        var unit = new UnitLayout();
        unit.Voxels.Bound = Bound.FromCorners(new Float3(0, 0, 0), new Float3(100, 100, 100));

        var a = Box("box", 1, 1, 1);
        Assert.True(unit.Add(a));
        Assert.Equal(new Float3(0, 0, 0), a.Transform.Position); // Bound 底格 (0,0,0)

        var b = Box("box", 1, 1, 1);
        Assert.True(unit.Add(b)); // (0,0,0) 已占，落到下一格
        Assert.Equal(new Float3(10, 0, 0), b.Transform.Position);
    }

    [Fact]
    public void Add_AutoPlaces_RestsOnImmutableFloor()
    {
        var unit = new UnitLayout();
        unit.Voxels.Bound = Bound.FromCorners(new Float3(0, 0, 0), new Float3(50, 50, 50));
        unit.Add(StructuralBox("floor", 5, 1, 5), new Position(new Float3(0, 0, 0))); // 铺满底 5×5

        var box = Box("box", 1, 1, 1);
        Assert.True(unit.Add(box));
        Assert.Equal(new Float3(0, 10, 0), box.Transform.Position); // 落到地板顶面 (0,1,0)
    }

    [Fact]
    public void Add_AutoPlaces_NowhereToStand_ReturnsFalse()
    {
        var unit = new UnitLayout();
        unit.Voxels.Bound = Bound.FromCorners(new Float3(0, 0, 0), new Float3(10, 10, 10)); // 2×2×2 格
        unit.Add(Box("box", 2, 2, 2), new Position(new Float3(0, 0, 0))); // 占满全部 8 格

        Assert.False(unit.Add(Box("box", 1, 1, 1))); // 无处可放
    }

    [Fact]
    public void Add_AutoPlaces_NoBound_ReturnsFalse()
    {
        var unit = new UnitLayout();
        Assert.False(unit.Add(Box("box", 1, 1, 1)));
    }

    // ── 表面附着（Add(Entity, Position, PlacementLayoutSource)）────

    [Fact]
    public void Add_OnSurface_RegistersHostAndVoxels()
    {
        var unit = new UnitLayout();
        var floor = new FloorEntity();
        unit.Add(floor, new Position(new Float3(0, 0, 0)));
        var source = floor.GetComponent<PlacementLayoutSource>()!;

        var mug = Box("mug", 1, 1, 1);
        Assert.True(unit.Add(mug, new Position(new Float3(0, 10, 0)), source));
        Assert.Same(mug, Assert.Single(source.Entities)); // 表面宿主登记
        Assert.True(unit.Voxels[new Int3(0, 1, 0)] is not null); // 体素投影
    }

    [Fact]
    public void Add_OnSurface_RejectsAlreadyPlacedEntity()
    {
        // 已放置实体（其表面组件可能被再次选中为目标）→ 拒绝：防"宿主即自身" / 重复放置
        var unit = new UnitLayout();
        var floor = new FloorEntity();
        unit.Add(floor, new Position(new Float3(0, 0, 0)));
        var source = floor.GetComponent<PlacementLayoutSource>()!;

        // 宿主即自身：floor 已放置，以自身表面为目标
        Assert.False(unit.Add(floor, new Position(new Float3(0, 10, 0)), source));

        // 重复放置：mug 已放置到 floor 表面，再次放置被拒绝
        var mug = Box("mug", 1, 1, 1);
        Assert.True(unit.Add(mug, new Position(new Float3(0, 10, 0)), source));
        Assert.False(unit.Add(mug, new Position(new Float3(10, 10, 0)), source));
    }

    [Fact]
    public void Add_OnSurface_AcceptsTiltedSurface()
    {
        // 斜表面（坡屋顶）可正常附着——体素占位轴对齐，与表面姿态无关
        var unit = new UnitLayout();
        var roof = new FloorEntity();
        roof.GetComponent<PlacementLayoutSource>()!.Transform = new Transform(new Float3(0, 10, 0), Rotation.Roof45);
        unit.Add(roof, new Position(new Float3(0, 0, 0)));
        var source = roof.GetComponent<PlacementLayoutSource>()!;

        var panel = Box("solar", 1, 1, 1); // 期望 Tilted（太阳能板）
        panel.AddProperties(new[] { new EnumProperty<RotationAlignment>(Property.RotationAlignment, RotationAlignment.Tilted) });
        Assert.True(unit.Add(panel, new Position(new Float3(0, 10, 0)), source));
        Assert.Same(panel, Assert.Single(source.Entities));
    }

    [Fact]
    public void Add_OnSurface_DefaultAlignmentIsUpside()
    {
        // 未配置期望类别 → 缺省 Upside：只可放朝上水平面
        var unit = new UnitLayout();
        var floor = new FloorEntity(); // Up 面
        unit.Add(floor, new Position(new Float3(0, 0, 0)));
        var upSource = floor.GetComponent<PlacementLayoutSource>()!;

        Assert.True(unit.Add(Box("mug", 1, 1, 1), new Position(new Float3(0, 10, 0)), upSource));

        // 朝下表面（天花板底面）→ 缺省 Upside 拒绝
        var ceiling = new FloorEntity();
        ceiling.GetComponent<PlacementLayoutSource>()!.Transform = new Transform(new Float3(0, 10, 0), Rotation.Down);
        unit.Add(ceiling, new Position(new Float3(50, 0, 0)));
        var downSource = ceiling.GetComponent<PlacementLayoutSource>()!;
        Assert.False(unit.Add(Box("lamp", 1, 1, 1), new Position(new Float3(50, 10, 0)), downSource));
    }

    [Fact]
    public void Add_OnSurface_RejectsMismatchedAlignment()
    {
        var unit = new UnitLayout();
        var floor = new FloorEntity(); // Up 面 → Upside
        unit.Add(floor, new Position(new Float3(0, 0, 0)));
        var source = floor.GetComponent<PlacementLayoutSource>()!;

        var painting = Box("painting", 1, 1, 1); // 期望 Vertical（墙面）
        painting.AddProperties(new[] { new EnumProperty<RotationAlignment>(Property.RotationAlignment, RotationAlignment.Vertical) });
        Assert.False(unit.Add(painting, new Position(new Float3(0, 10, 0)), source));
    }

    [Fact]
    public void Add_OnSurface_HorizontalAcceptsUpFacing()
    {
        var unit = new UnitLayout();
        var floor = new FloorEntity(); // Up 面
        unit.Add(floor, new Position(new Float3(0, 0, 0)));
        var source = floor.GetComponent<PlacementLayoutSource>()!;

        var mat = Box("mat", 1, 1, 1); // 期望 Horizontal → 接受 Upside 面
        mat.AddProperties(new[] { new EnumProperty<RotationAlignment>(Property.RotationAlignment, RotationAlignment.Horizontal) });
        Assert.True(unit.Add(mat, new Position(new Float3(0, 10, 0)), source));
    }

    // ── 删除（Remove 连带回落表面物件）───────────────

    [Fact]
    public void Remove_RemovesWholeChain()
    {
        var unit = new UnitLayout();
        var floor = new FloorEntity();
        unit.Add(floor, new Position(new Float3(0, 0, 0)));
        var floorSurface = floor.GetComponent<PlacementLayoutSource>()!;

        var mug = Box("mug", 1, 1, 1);
        mug.AddComponent(Surface(new Int2(1, 1)));
        unit.Add(mug, new Position(new Float3(0, 10, 0)), floorSurface);

        var lid = Box("lid", 1, 1, 1);
        unit.Add(lid, new Position(new Float3(0, 20, 0)), mug.GetComponent<PlacementLayoutSource>()!);

        Assert.True(unit.Remove(floor));
        Assert.Empty(unit.Entities); // 链上全部回落
        Assert.Empty(floorSurface.Entities);
        Assert.Empty(mug.GetComponent<PlacementLayoutSource>()!.Entities);
        Assert.True(unit.Voxels[new Int3(0, 0, 0)] is null);
        Assert.True(unit.Voxels[new Int3(0, 2, 0)] is null);
    }

    [Fact]
    public void Remove_RemovesHostAndItsItems()
    {
        // 语义单一：删宿主连带回落其表面物件（实体不能悬空）——删除前确认是编辑器 UI 的职责
        var unit = new UnitLayout();
        var floor = new FloorEntity();
        unit.Add(floor, new Position(new Float3(0, 0, 0)));
        var source = floor.GetComponent<PlacementLayoutSource>()!;
        unit.Add(Box("mug", 1, 1, 1), new Position(new Float3(0, 10, 0)), source);

        Assert.True(unit.Remove(floor));
        Assert.Empty(unit.Entities); // 宿主 + 表面物件全部回落
        Assert.Empty(source.Entities);
        Assert.True(unit.Voxels[new Int3(0, 1, 0)] is null);
    }

    [Fact]
    public void Remove_UnregistersFromHostSurface()
    {
        var unit = new UnitLayout();
        var floor = new FloorEntity();
        unit.Add(floor, new Position(new Float3(0, 0, 0)));
        var source = floor.GetComponent<PlacementLayoutSource>()!;
        var mug = Box("mug", 1, 1, 1);
        unit.Add(mug, new Position(new Float3(0, 10, 0)), source);

        Assert.True(unit.Remove(mug));
        Assert.Empty(source.Entities); // 反登记：宿主表面不再引用
        Assert.True(unit.Voxels[new Int3(0, 1, 0)] is null);
        Assert.Contains(floor, unit.Entities); // 宿主保留
    }

    [Fact]
    public void HostOf_ReturnsSurfaceForHostedAndNullForRoot()
    {
        var unit = new UnitLayout();
        var floor = new FloorEntity();
        unit.Add(floor, new Position(new Float3(0, 0, 0)));
        var source = floor.GetComponent<PlacementLayoutSource>()!;
        var mug = Box("mug", 1, 1, 1);
        unit.Add(mug, new Position(new Float3(0, 10, 0)), source);

        Assert.Same(source, unit.FindHostEntity(mug)); // 附着物件 → 宿主表面
        Assert.Null(unit.FindHostEntity(floor)); // 根物件 → null

        unit.Remove(mug);
        Assert.Null(unit.FindHostEntity(mug)); // 删除后消解
    }

    [Fact]
    public void Remove_NoHostCycleBecauseAlreadyPlacedRejected()
    {
        // 环的最后一步（已放置实体再次附着）被 Add 拒绝 → Items 恒为森林，删除递归不环
        var unit = new UnitLayout();
        var floor = new FloorEntity();
        unit.Add(floor, new Position(new Float3(0, 0, 0)));
        var source = floor.GetComponent<PlacementLayoutSource>()!;
        var mug = Box("mug", 1, 1, 1);
        mug.AddComponent(Surface(new Int2(1, 1)));
        unit.Add(mug, new Position(new Float3(0, 10, 0)), source);

        // mug 已放置 → 不能再附着到 floor 表面（反之亦然）
        Assert.False(unit.Add(mug, new Position(new Float3(0, 10, 0)), source));

        Assert.True(unit.Remove(floor)); // 连带回落正常终止
        Assert.Empty(unit.Entities);
    }

    [Fact]
    public void Rebuild_RasterizesEntitiesBackIntoGrid()
    {
        var unit = new UnitLayout();
        var entity = Box("box", 2, 1, 2);
        unit.Add(entity, new Position(new Float3(50, 0, 50)));

        // 直接低层写入一个游离引用（绕过同步）
        unit.Voxels[new Int3(0, 0, 0)] = entity;

#pragma warning disable CS0618 // Rebuild 已弃用（待重写）；此测试验证重写前的既有行为
        unit.Rebuild();
#pragma warning restore CS0618

        Assert.True(unit.Voxels[new Int3(0, 0, 0)] is null); // 游离引用被清除
        Assert.True(unit.Voxels[new Int3(5, 0, 5)] is not null);
        Assert.True(unit.Voxels[new Int3(6, 0, 6)] is not null);
    }
}
