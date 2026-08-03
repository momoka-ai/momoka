using Xunit;
using Momoka.Home;
using Momoka.Home.Entities;
using Momoka.Home.Layouts;
using Momoka.Home.Levels;
using Momoka.Home.Primitives;
namespace Momoka.Home.Tests.Models.Levels;

/// <summary>
/// End-to-end checks of the level as the unified placement-surface catalog:
/// Level : IVoxelLayout2DSource aggregates its floor/ceiling surfaces and every
/// wall's two faces, and BuildWall rasterizes a wall into the occupancy grid.
/// </summary>
public class LevelTests
{
    [Fact]
    public void Layouts_IncludesFloorCeilingAndWallFaces()
    {
        var level = new Level();
        var wall = new Wall();
        level.Boundary.Build(new Int2(2, 0), new Int2(7, 0), wall);
        level.Layout.BuildAt(wall, new Int3(2, 0, 0));

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
    public void Floor_IsAPlaneLayoutWithMaterialSubdivision()
    {
        var level = new Level();
        Assert.IsType<PlaneLayout<TileEntity>>(level.Floor);
        Assert.NotNull(level.Floor.Subdivision);
    }

    [Fact]
    public void Build_AndBuildAt_PopulateGridAndBoundary()
    {
        var level = new Level();
        var wall = new Wall();
        Assert.True(level.Boundary.Build(new Int2(2, 0), new Int2(7, 0), wall));
        Assert.True(level.Layout.BuildAt(wall, new Int3(2, 0, 0)));

        var registered = Assert.Single(level.Layout.GetEntitiesOfType<Wall>());
        Assert.Equal(new Int3(2, 0, 0), registered.Coords);

        // voxels occupy (2..7, 0, 0) — anchored at Coords + local
        Assert.True(level.Layout.HasEntity(new Int3(2, 0, 0)));
        Assert.True(level.Layout.HasEntity(new Int3(7, 0, 0)));
        Assert.False(level.Layout.HasEntity(new Int3(1, 0, 0)));

        // boundary nodes registered
        Assert.NotNull(level.Boundary.TryGetNode(new Int2(2, 0)));
        Assert.NotNull(level.Boundary.TryGetNode(new Int2(7, 0)));
    }
}
