using Momoka.Home;
using Momoka.Home.Primitives;
using Xunit;

namespace Momoka.Home.Tests.Models.Layouts;

/// <summary>
/// GraphLayout2D = boundary partition graph with build/demolish. Supports any
/// LineShape partition (wall, fence…), not just Wall.
/// </summary>
public class GraphLayout2DTests
{
    private sealed class FenceEntity : VoxelEntity
    {
        public FenceEntity() => Shape = new LineShape();
    }

    [Fact]
    public void BuildPartition_RegistersEdgeAndOccupancy()
    {
        var layout = new VoxelLayout3D();
        var graph = new GraphLayout2D(layout);
        var wall = new Wall();

        Assert.True(graph.BuildPartition(new Int2(2, 0), new Int2(7, 0), wall));
        Assert.Equal(new Int3(2, 0, 0), wall.Coords);
        Assert.NotNull(graph.TryGetNode(new Int2(2, 0)));
        Assert.NotNull(graph.TryGetNode(new Int2(7, 0)));
        Assert.True(layout.HasEntity(new Int3(2, 0, 0)));
        Assert.True(layout.HasEntity(new Int3(7, 0, 0)));
        Assert.Single(layout.Entities);
    }

    [Fact]
    public void BuildPartition_WorksWithAnyLineShapeEntity()
    {
        var layout = new VoxelLayout3D();
        var graph = new GraphLayout2D(layout);

        Assert.True(graph.BuildPartition(new Int2(0, 0), new Int2(0, 5), new FenceEntity()));
        Assert.Single(layout.Entities);
    }

    [Fact]
    public void BuildPartition_NonLineShape_ReturnsFalse()
    {
        var layout = new VoxelLayout3D();
        var graph = new GraphLayout2D(layout);
        var box = new Appliance(); // BoxShape

        Assert.False(graph.BuildPartition(new Int2(0, 0), new Int2(3, 0), box));
        Assert.Empty(layout.Entities);
    }

    [Fact]
    public void DemolishPartition_ClearsEdgeOccupancyAndEntity()
    {
        var layout = new VoxelLayout3D();
        var graph = new GraphLayout2D(layout);
        var wall = new Wall();
        graph.BuildPartition(new Int2(2, 0), new Int2(7, 0), wall);

        Assert.True(graph.DemolishPartition(new Int2(2, 0), new Int2(7, 0)));
        Assert.False(layout.HasEntity(new Int3(2, 0, 0)));
        Assert.False(layout.HasEntity(new Int3(7, 0, 0)));
        Assert.Empty(layout.Entities);
        Assert.False(graph.DemolishPartition(new Int2(2, 0), new Int2(7, 0))); // 已拆除
    }
}
