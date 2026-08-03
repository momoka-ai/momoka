using Xunit;
using Momoka.Home;
using Momoka.Home.Entities;
using Momoka.Home.Layouts;
using Momoka.Home.Primitives;
using Momoka.Home.Shapes;
namespace Momoka.Home.Tests.Models.Layouts;

/// <summary>
/// VoxelLayout3D owns the 3D occupancy container: construction writes ALL of an
/// entity's voxels, destruction clears them, and the entity list stays in sync.
/// </summary>
public class VoxelLayout3DTests
{
    private sealed class TestEntity : VoxelEntity
    {
        public TestEntity(Shape shape) => Shape = shape;
    }

    private static TestEntity MakeBox(int sx, int sz) =>
        new(new BoxShape { SizeX = sx, SizeY = 1, SizeZ = sz });

    [Fact]
    public void ConstructAt_WritesAllVoxels_AndRegisters()
    {
        var layout = new VoxelLayout3D();
        var entity = MakeBox(2, 2);

        Assert.True(layout.ConstructAt(entity, new Int3(5, 0, 5)));
        Assert.Equal(new Int3(5, 0, 5), entity.Coords);

        // 全部 4 个体素格都写入（不只锚点）
        Assert.True(layout.HasEntity(new Int3(5, 0, 5)));
        Assert.True(layout.HasEntity(new Int3(6, 0, 5)));
        Assert.True(layout.HasEntity(new Int3(5, 0, 6)));
        Assert.True(layout.HasEntity(new Int3(6, 0, 6)));
        Assert.Same(entity, layout.FindEntity(entity.Id));
    }

    [Fact]
    public void IsEntityCollided_TrueWhenAnchorOccupied()
    {
        var layout = new VoxelLayout3D();
        layout.ConstructAt(MakeBox(1, 1), new Int3(5, 0, 5));

        Assert.True(layout.IsEntityCollided(MakeBox(1, 1), new Int3(5, 0, 5)));
        Assert.False(layout.ConstructAt(MakeBox(1, 1), new Int3(5, 0, 5)));
    }

    [Fact]
    public void IsEntityCollided_TrueWhenVoxelsOverlap_EvenIfAnchorsDiffer()
    {
        var layout = new VoxelLayout3D();
        layout.ConstructAt(MakeBox(2, 2), new Int3(5, 0, 5)); // 占用 (5..6, 5..6)

        // B 锚点 (6,0,5) 不同，但体素 (6,0,5)/(6,0,6) 与 A 重叠
        Assert.True(layout.IsEntityCollided(MakeBox(2, 2), new Int3(6, 0, 5)));
        Assert.False(layout.ConstructAt(MakeBox(2, 2), new Int3(6, 0, 5)));
    }

    [Fact]
    public void IsEntityCollided_WithSpecificDest()
    {
        var layout = new VoxelLayout3D();
        var dest = MakeBox(2, 2);
        layout.ConstructAt(dest, new Int3(5, 0, 5));

        var src = MakeBox(1, 1);
        Assert.True(layout.IsEntityCollided(dest, src, new Int3(6, 0, 5))); // 命中 dest 体素
        Assert.False(layout.IsEntityCollided(dest, src, new Int3(9, 0, 9))); // 不重叠
    }

    [Fact]
    public void ConstructAt_NextToEntity_Succeeds()
    {
        var layout = new VoxelLayout3D();
        layout.ConstructAt(MakeBox(1, 1), new Int3(5, 0, 5));

        Assert.True(layout.ConstructAt(MakeBox(1, 1), new Int3(7, 0, 5)));
        Assert.Equal(2, layout.Entities.Count);
    }

    [Fact]
    public void DestructAt_RemovesEntityByRegisteredPosition()
    {
        var layout = new VoxelLayout3D();
        layout.ConstructAt(MakeBox(2, 2), new Int3(5, 0, 5));

        Assert.True(layout.DestructAt(new Int3(5, 0, 5)));
        Assert.False(layout.HasEntity(new Int3(6, 0, 6)));
        Assert.Empty(layout.Entities);
        Assert.False(layout.DestructAt(new Int3(5, 0, 5))); // 已移除
    }

    [Fact]
    public void DestructTarget_RemovesEntityCoveringAnyCell()
    {
        var layout = new VoxelLayout3D();
        layout.ConstructAt(MakeBox(2, 2), new Int3(5, 0, 5));

        Assert.True(layout.DestructTarget(new Int3(6, 0, 6))); // 非锚点格
        Assert.Empty(layout.Entities);
        Assert.False(layout.HasEntity(new Int3(5, 0, 5)));
    }

    [Fact]
    public void FlushVoxelEntities_RebuildsGridFromEntityList()
    {
        var layout = new VoxelLayout3D();
        var entity = MakeBox(2, 2);
        layout.ConstructAt(entity, new Int3(5, 0, 5));

        // 直接低层写入一个未注册实体（绕过同步）
        var stray = MakeBox(1, 1);
        layout[new Int3(0, 0, 0)] = stray;

        layout.FlushVoxelEntities();

        // 未注册实体被清除
        Assert.False(layout.HasEntity(new Int3(0, 0, 0)));
        // 已注册实体重新栅格化
        Assert.True(layout.HasEntity(new Int3(5, 0, 5)));
        Assert.True(layout.HasEntity(new Int3(6, 0, 6)));
    }

    [Fact]
    public void GetEntitiesInBound_FindsEntityInBox()
    {
        var layout = new VoxelLayout3D();
        layout.ConstructAt(MakeBox(2, 2), new Int3(5, 0, 5));

        Assert.Single(layout.GetEntitiesInBound(new Int2(5, 5), new Int2(6, 6)));
        Assert.Empty(layout.GetEntitiesInBound(new Int2(0, 0), new Int2(1, 1)));
    }
}
