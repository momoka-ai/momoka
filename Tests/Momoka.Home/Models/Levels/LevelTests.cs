using Xunit;
using Momoka.Home;
using Momoka.Home.Components;
using Momoka.Home.Entities;
using Momoka.Home.Layouts;
using Momoka.Home.Levels;
using Momoka.Home.Primitives;
using Momoka.Home.Shapes;
namespace Momoka.Home.Tests.Models.Levels;

/// <summary>
/// End-to-end checks of the level as the unified placement-surface catalog:
/// the level aggregates its floor/ceiling planes and every contained entity's
/// VoxelLayoutSource component (walls' faces and any other surface source placed
/// in the occupancy grid).
/// </summary>
public class LevelTests
{
    private sealed class VoxelLayoutSourceEntity : Entity<Int3>
    {
        public VoxelLayoutSourceEntity()
        {
            Shape = new BoxShape();
            AddComponent(new VoxelLayoutSource { Layouts = { new VoxelLayout2D(new Int2(2, 2)) } });
        }
    }
    [Fact]
    public void Layouts_IncludesFloorCeilingAndWallFaces()
    {
        var level = new Level();
        var wall = new Wall();
        level.Plan.Build(new Int2(2, 0), new Int2(7, 0), wall);
        level.Layout.BuildAt(wall, new Int3(2, 0, 0));
        wall.RefreshSurfaces(); // 构建命令负责把派生表面物化进组件

        // floor + ceiling + wall's two faces (E-W wall → south + north)
        Assert.Equal(4, level.Layouts.Count());
    }

    [Fact]
    public void Layouts_WithoutWalls_ContainsOnlyFloorAndCeiling()
    {
        var level = new Level();
        Assert.Equal(2, level.Layouts.Count());
    }

    [Fact]
    public void Layouts_IncludesAnyVoxelLayoutSourceEntity_NotJustWalls()
    {
        var level = new Level();
        var source = new VoxelLayoutSourceEntity();
        level.Layout.BuildAt(source, new Int3(2, 0, 0));

        // floor + ceiling + the custom surface-source entity's surface
        Assert.Equal(3, level.Layouts.Count());
        var surface = source.GetComponent<VoxelLayoutSource>()!.Layouts.Single();
        Assert.Contains(surface, level.Layouts);
    }

    [Fact]
    public void Floor_IsAPlaneLayoutWithMaterialSubdivision()
    {
        var level = new Level();
        Assert.IsType<PlaneLayout<Entity<Int2>>>(level.Floor);
        Assert.NotNull(level.Floor.Subdivision);
    }

    [Fact]
    public void Build_AndBuildAt_PopulateGridAndBoundary()
    {
        var level = new Level();
        var wall = new Wall();
        Assert.True(level.Plan.Build(new Int2(2, 0), new Int2(7, 0), wall));
        Assert.True(level.Layout.BuildAt(wall, new Int3(2, 0, 0)));

        var registered = Assert.Single(level.Layout.GetEntitiesOfType<Wall>());
        Assert.Equal(new Int3(2, 0, 0), registered.Coords);

        // voxels occupy (2..7, 0, 0) — anchored at Coords + local
        Assert.True(level.Layout.HasEntity(new Int3(2, 0, 0)));
        Assert.True(level.Layout.HasEntity(new Int3(7, 0, 0)));
        Assert.False(level.Layout.HasEntity(new Int3(1, 0, 0)));

        // boundary nodes registered
        Assert.NotNull(level.Plan.TryGetNode(new Int2(2, 0)));
        Assert.NotNull(level.Plan.TryGetNode(new Int2(7, 0)));
    }
}
