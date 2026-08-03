using Momoka.Home.Models.Entities;
using Momoka.Home.Models.Layouts;
using Momoka.Home.Models.Shapes;
using Momoka.Home.Primitives;
using Xunit;

namespace Momoka.Home.Tests.Models.Layouts;

/// <summary>
/// VoxelLayout3D owns the occupancy container: cell storage + entity list stay
/// in sync through Place/Remove; queries go through the same container.
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
    public void Place_SetsCoordsAndRegistersEntity()
    {
        var layout = new VoxelLayout3D();
        var entity = MakeBox(2, 2);

        Assert.True(layout.Place(entity, new Int3(5, 0, 5)));
        Assert.Equal(new Int3(5, 0, 5), entity.Coords);
        Assert.True(layout.HasEntity(new Int3(5, 0, 5)));
        Assert.Same(entity, layout.GetEntityAtPoint(new Int3(5, 0, 5)));
        Assert.Same(entity, layout.FindEntity(entity.Id));
        Assert.Single(layout.GetEntitiesOfType<TestEntity>());
    }

    [Fact]
    public void CanPlace_FalseWhenAnchorOccupied()
    {
        var layout = new VoxelLayout3D();
        layout.Place(MakeBox(1, 1), new Int3(5, 0, 5));

        Assert.False(layout.CanPlace(MakeBox(1, 1), new Int3(5, 0, 5)));
        Assert.False(layout.Place(MakeBox(1, 1), new Int3(5, 0, 5)));
    }

    [Fact]
    public void CanPlace_FalseWhenShapeVoxelHitsAnAnchor()
    {
        var layout = new VoxelLayout3D();
        layout.Place(MakeBox(2, 2), new Int3(5, 0, 5)); // 锚点 (5,0,5)

        // 2×2 从 (4,0,5) 开始：体素含 (5,0,5) = 已有实体的锚点
        Assert.False(layout.CanPlace(MakeBox(2, 2), new Int3(4, 0, 5)));
    }

    [Fact]
    public void Place_NextToEntity_Succeeds()
    {
        var layout = new VoxelLayout3D();
        layout.Place(MakeBox(1, 1), new Int3(5, 0, 5));

        Assert.True(layout.CanPlace(MakeBox(1, 1), new Int3(7, 0, 5)));
        Assert.True(layout.Place(MakeBox(1, 1), new Int3(7, 0, 5)));
        Assert.Equal(2, layout.Entities.Count);
    }

    [Fact]
    public void Remove_ClearsEntityAndOwnedCells()
    {
        var layout = new VoxelLayout3D();
        var entity = MakeBox(1, 1);
        layout.Place(entity, new Int3(5, 0, 5));

        Assert.True(layout.Remove(entity));
        Assert.False(layout.HasEntity(new Int3(5, 0, 5)));
        Assert.Empty(layout.Entities);
        Assert.Null(layout.FindEntity(entity.Id));
    }

    [Fact]
    public void GetEntitiesInBound_FindsEntityInBox()
    {
        var layout = new VoxelLayout3D();
        layout.Place(MakeBox(2, 2), new Int3(5, 0, 5));

        Assert.Single(layout.GetEntitiesInBound(new Int2(5, 5), new Int2(6, 6)));
        Assert.Empty(layout.GetEntitiesInBound(new Int2(0, 0), new Int2(1, 1)));
    }
}
