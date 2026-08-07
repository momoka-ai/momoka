using Xunit;
using Momoka.Home;
using Momoka.Home.Entities;
using Momoka.Home.Geometry;
using Momoka.Home.Layouts;
using Momoka.Home.Primitives;
namespace Momoka.Home.Tests.Models.Layouts;

/// <summary>
/// FloorPlanLayout = boundary partition graph: positioning an edge entity and
/// registering its nodes/edge (TOPOLOGY), plus on-demand placement surfaces
/// (<see cref="FloorPlanLayout.Surfaces"/>) derived from the edge span and the
/// partition's property table. Occupancy rasterization is VoxelLayout&lt;Entity&gt;'s job.
/// </summary>
public class FloorPlanLayoutTests
{
    private sealed class FenceEntity : Entity
    {
        public FenceEntity() => Volume = new Line3D();
    }

    // ── Topology ─────────────────────────────────────────────

    [Fact]
    public void Build_PositionsPartitionAndRegistersEdge()
    {
        var graph = new FloorPlanLayout();
        var wall = new Wall();

        Assert.True(graph.Build(new Int2(2, 0), new Int2(7, 0), wall));
        Assert.Equal(new Int3(2, 0, 0), wall.Coords);
        Assert.NotNull(graph.TryGetNode(new Int2(2, 0)));
        Assert.NotNull(graph.TryGetNode(new Int2(7, 0)));
        Assert.Single(graph.Edges);
    }

    [Fact]
    public void Build_WorksWithAnyLine3DEntity()
    {
        var graph = new FloorPlanLayout();

        Assert.True(graph.Build(new Int2(0, 0), new Int2(0, 5), new FenceEntity()));
        Assert.Single(graph.Edges);
    }

    [Fact]
    public void Build_NonLine3D_ReturnsFalse()
    {
        var graph = new FloorPlanLayout();

        Assert.False(graph.Build(new Int2(0, 0), new Int2(3, 0), new Appliance()));
        Assert.Empty(graph.Edges);
    }

    [Fact]
    public void Destroy_RemovesEdge()
    {
        var graph = new FloorPlanLayout();
        var wall = new Wall();
        graph.Build(new Int2(2, 0), new Int2(7, 0), wall);

        Assert.True(graph.Destroy(new Int2(2, 0), new Int2(7, 0)));
        Assert.Empty(graph.Edges);
        Assert.False(graph.Destroy(new Int2(2, 0), new Int2(7, 0))); // 已拆除
    }

    // ── Placement surfaces (方案 1: plan-derived, property-table config) ──

    [Fact]
    public void Surfaces_EastWestWall_ExposesSouthAndNorthFaces()
    {
        var plan = new FloorPlanLayout();
        var wall = new Wall(); // use_voxel_layout = true, height = 3, thickness = 1
        plan.Build(new Int2(2, 0), new Int2(7, 0), wall); // 长 5

        var faces = plan.Surfaces.ToList();

        Assert.Equal(2, faces.Count);
        Assert.Contains(faces, f => f.Direction == Int3.South);
        Assert.Contains(faces, f => f.Direction == Int3.North);

        var south = faces.First(f => f.Direction == Int3.South);
        Assert.Equal(new Int2(5, 3), south.Size); // 长 × 高（属性表）
        Assert.Equal(new Int3(2, 0, 0), south.Offset);

        var north = faces.First(f => f.Direction == Int3.North);
        Assert.Equal(new Int3(2, 0, 1), north.Offset); // 厚度 1 → +Z 侧
    }

    [Fact]
    public void Surfaces_NorthSouthWall_ExposesWestAndEastFaces()
    {
        var plan = new FloorPlanLayout();
        var wall = new Wall();
        wall.SetValue(FloorPlanLayout.HeightProperty, 4); // 属性表驱动高度（按名，作用于实例克隆）
        plan.Build(new Int2(0, 2), new Int2(0, 6), wall); // 长 4

        var faces = plan.Surfaces.ToList();

        Assert.Equal(2, faces.Count);
        Assert.Contains(faces, f => f.Direction == Int3.West);
        Assert.Contains(faces, f => f.Direction == Int3.East);

        var west = faces.First(f => f.Direction == Int3.West);
        Assert.Equal(new Int2(4, 4), west.Size); // 高 × 长
        Assert.Equal(new Int3(0, 0, 2), west.Offset);

        var east = faces.First(f => f.Direction == Int3.East);
        Assert.Equal(new Int3(1, 0, 2), east.Offset); // 厚度 1 → +X 侧
    }

    [Fact]
    public void Surfaces_HeightDrivenByPropertyTable()
    {
        var plan = new FloorPlanLayout();
        var wall = new Wall();
        wall.SetValue(FloorPlanLayout.HeightProperty, 6);
        plan.Build(new Int2(2, 0), new Int2(7, 0), wall);

        var south = plan.Surfaces.First(f => f.Direction == Int3.South);
        Assert.Equal(new Int2(5, 6), south.Size); // 长 × 高（属性表）
    }

    [Fact]
    public void Surfaces_ThicknessDrivenByPropertyTable()
    {
        var plan = new FloorPlanLayout();
        var wall = new Wall();
        wall.SetValue(FloorPlanLayout.ThicknessProperty, 2);
        plan.Build(new Int2(2, 0), new Int2(7, 0), wall);

        var north = plan.Surfaces.First(f => f.Direction == Int3.North);
        Assert.Equal(new Int3(2, 0, 2), north.Offset); // 厚度 2 → +Z 侧
    }

    [Fact]
    public void Surfaces_SkipsPartitionWithoutUseVoxelLayout()
    {
        var plan = new FloorPlanLayout();
        plan.Build(new Int2(0, 0), new Int2(0, 5), new FenceEntity()); // 无 use_voxel_layout 属性

        Assert.Empty(plan.Surfaces);
    }

    [Fact]
    public void Surfaces_DiagonalWall_HasNoFaces()
    {
        var plan = new FloorPlanLayout();
        var wall = new Wall();
        plan.Build(new Int2(0, 0), new Int2(3, 5), wall); // 斜墙：无轴对齐法线

        Assert.Empty(plan.Surfaces);
    }

    [Fact]
    public void Surfaces_FaceToWorld_MapsOntoTheWallPlane()
    {
        var plan = new FloorPlanLayout();
        var wall = new Wall();
        plan.Build(new Int2(2, 0), new Int2(7, 0), wall);

        var north = plan.Surfaces.First(f => f.Direction == Int3.North);

        // 北面：local.X→世界X（沿墙），local.Z→世界Y（高度）
        Assert.Equal(new Int3(2, 0, 1), north.AsAbsolute(new Int2(0, 0)));
        Assert.Equal(new Int3(6, 2, 1), north.AsAbsolute(new Int2(4, 2)));
    }
}
