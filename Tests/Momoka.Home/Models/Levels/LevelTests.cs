using Momoka.Home.Primitives;
using Xunit;

using Momoka.Home;
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
        level.Boundary.BuildPartition(new Int2(2, 0), new Int2(7, 0), new Wall());

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
    public void BuildPartition_PopulatesGridAndBoundary()
    {
        var level = new Level();
        Assert.True(level.Boundary.BuildPartition(new Int2(2, 0), new Int2(7, 0), new Wall()));

        var wall = Assert.Single(level.Layout.GetEntitiesOfType<Wall>());
        Assert.Equal(new Int3(2, 0, 0), wall.Coords);

        // voxels occupy (2..7, 0, 0) — anchored at Coords + local
        Assert.True(level.Layout.HasEntity(new Int3(2, 0, 0)));
        Assert.True(level.Layout.HasEntity(new Int3(7, 0, 0)));
        Assert.False(level.Layout.HasEntity(new Int3(1, 0, 0)));

        // boundary nodes registered
        Assert.NotNull(level.Boundary.TryGetNode(new Int2(2, 0)));
        Assert.NotNull(level.Boundary.TryGetNode(new Int2(7, 0)));
    }
}
