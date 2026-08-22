using Newtonsoft.Json;
using Xunit;
using Momoka.Home.Data;
using Momoka.Home;
using Momoka.Home.Levels.Volumes;
using Momoka.Home.Primitives;
namespace Momoka.Home.Tests.Models.Shapes;

/// <summary>LineGraph：线图墙体积的几何、节点/边表与序列化。</summary>
public class LineGraph3DTests
{
    [Fact]
    public void SingleSegment_ProducesWallBox()
    {
        var wall = new LineGraph { Height = 29 };
        wall.AddSegment(new Int3(0, 0, 0), new Int3(9, 0, 0), thickness: 1);

        var cells = wall.GetVoxelSet().ToList();
        Assert.Equal(10 * 29, cells.Count); // 10 长 × 29 高 × 1 厚
        Assert.Contains(new Int3(0, 0, 0), cells);
        Assert.Contains(new Int3(9, 28, 0), cells);
        Assert.All(cells, c => Assert.Equal(0, c.Z)); // 仅中心线格

        Assert.Equal(2, wall.Nodes.Count);
        Assert.Single(wall.Edges);
        Assert.Single(wall.Children);
        Assert.All(wall.Children, c => Assert.IsType<Line>(c.Shape));
    }

    [Fact]
    public void LShape_SharesCornerNode_UnionConnected()
    {
        var wall = new LineGraph { Height = 10 };
        wall.AddSegment(new Int3(0, 0, 0), new Int3(4, 0, 0), 1); // X 方向
        wall.AddSegment(new Int3(4, 0, 0), new Int3(4, 0, 4), 1); // Z 方向（共享转角节点）

        var cells = wall.GetVoxelSet().ToHashSet();
        Assert.Equal((5 + 5 - 1) * wall.Height, cells.Count); // 每层并集去重 × 高度
        Assert.Contains(new Int3(0, 0, 0), cells);
        Assert.Contains(new Int3(4, 0, 0), cells); // 转角格
        Assert.Contains(new Int3(4, 0, 4), cells);

        Assert.Equal(3, wall.Nodes.Count); // 共享节点去重
        Assert.Contains(new Int3(4, 0, 0), wall.Nodes);
        Assert.Equal(2, wall.Edges.Count);
        Assert.Equal(2, wall.Children.Count);
    }

    [Fact]
    public void AddSegment_DedupsSharedNode_IndicesAlignWithChildren()
    {
        var wall = new LineGraph();
        wall.AddSegment(new Int3(0, 0, 0), new Int3(5, 0, 0), 1);
        wall.AddSegment(new Int3(5, 0, 0), new Int3(9, 0, 0), 1);
        wall.AddSegment(new Int3(5, 0, 0), new Int3(5, 0, 5), 1);

        Assert.Equal(4, wall.Nodes.Count); // (0,0,0) (5,0,0) (9,0,0) (5,0,5)
        Assert.Equal(3, wall.Edges.Count);
        Assert.Equal(wall.Edges.Count, wall.Children.Count);
        Assert.Equal(new EdgeIndex(1, 2), wall.Edges[1]); // 第二边 → 节点 1→2
    }

    [Fact]
    public void RoundTrips_ThroughJson_WithGraphTable()
    {
        var wall = new LineGraph { Height = 29 };
        wall.AddSegment(new Int3(0, 0, 0), new Int3(9, 0, 0), 1);
        wall.AddSegment(new Int3(9, 0, 0), new Int3(9, 0, 6), 2);

        var json = JsonConvert.SerializeObject(wall, Settings.JsonSerialization);
        Assert.Contains("line_graph", json);

        var back = JsonConvert.DeserializeObject<LineGraph>(json, Settings.JsonSerialization)!;
        Assert.Equal(29, back.Height);
        Assert.Equal(wall.Nodes, back.Nodes);
        Assert.Equal(wall.Edges, back.Edges);
        Assert.Equal(2, back.Children.Count);
        Assert.All(back.Children, c => Assert.IsType<Line>(c.Shape));
        Assert.Equal(wall.GetVoxelSet().ToHashSet(), back.GetVoxelSet().ToHashSet());
    }
}
