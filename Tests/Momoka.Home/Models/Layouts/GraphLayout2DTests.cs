using Xunit;
using Momoka.Home;
using Momoka.Home.Entities;
using Momoka.Home.Layouts;
using Momoka.Home.Primitives;
using Momoka.Home.Shapes;
namespace Momoka.Home.Tests.Models.Layouts;

/// <summary>
/// GraphLayout2D = boundary partition graph TOPOLOGY only: positioning an edge
/// entity and registering its nodes/edge. Occupancy rasterization is
/// VoxelLayout3D's job, coordinated by the caller.
/// </summary>
public class GraphLayout2DTests
{
    private sealed class FenceEntity : VoxelEntity
    {
        public FenceEntity() => Shape = new LineShape();
    }

    [Fact]
    public void Build_PositionsPartitionAndRegistersEdge()
    {
        var graph = new GraphLayout2D();
        var wall = new Wall();

        Assert.True(graph.Build(new Int2(2, 0), new Int2(7, 0), wall));
        Assert.Equal(new Int3(2, 0, 0), wall.Coords);
        Assert.NotNull(graph.TryGetNode(new Int2(2, 0)));
        Assert.NotNull(graph.TryGetNode(new Int2(7, 0)));
        Assert.Single(graph.Edges);
    }

    [Fact]
    public void Build_WorksWithAnyLineShapeEntity()
    {
        var graph = new GraphLayout2D();

        Assert.True(graph.Build(new Int2(0, 0), new Int2(0, 5), new FenceEntity()));
        Assert.Single(graph.Edges);
    }

    [Fact]
    public void Build_NonLineShape_ReturnsFalse()
    {
        var graph = new GraphLayout2D();

        Assert.False(graph.Build(new Int2(0, 0), new Int2(3, 0), new Appliance()));
        Assert.Empty(graph.Edges);
    }

    [Fact]
    public void Destroy_RemovesEdge()
    {
        var graph = new GraphLayout2D();
        var wall = new Wall();
        graph.Build(new Int2(2, 0), new Int2(7, 0), wall);

        Assert.True(graph.Destroy(new Int2(2, 0), new Int2(7, 0)));
        Assert.Empty(graph.Edges);
        Assert.False(graph.Destroy(new Int2(2, 0), new Int2(7, 0))); // 已拆除
    }
}
