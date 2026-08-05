using Xunit;
using Momoka.Home;
using Momoka.Home.Entities;
using Momoka.Home.Geometry;
using Momoka.Home.Layouts;
using Momoka.Home.Primitives;
namespace Momoka.Home.Tests.Models.Levels;

/// <summary>
/// Upward composition through <see cref="IVoxelGeometry3D"/>: Level → Building →
/// Home. Containers place their whole voxel occupancy into a parent layout at an
/// offset, and remove it again on demand.
/// </summary>
public class CompositionTests
{
    private static Level LevelWithWall(Int3 levelCoords)
    {
        var level = new Level { Coords = levelCoords };
        var wall = new Wall();
        level.Plan.Build(new Int2(2, 0), new Int2(7, 0), wall);
        level.Layout.BuildAt(wall, new Int3(2, 0, 0));
        return level;
    }

    [Fact]
    public void Level_PlaceAt_CopiesOccupancyIntoParentAtOffset()
    {
        var level = LevelWithWall(new Int3(0, 3, 0));

        var buildingLayout = new VoxelLayout3D();
        level.PlaceAt(buildingLayout, level.Coords);

        Assert.True(buildingLayout.HasEntity(new Int3(2, 3, 0)));
        Assert.True(buildingLayout.HasEntity(new Int3(7, 3, 0)));
        Assert.Equal(level.Layout.Entities, buildingLayout.Entities);
    }

    [Fact]
    public void Level_DestroyAt_RemovesOccupancyFromParent()
    {
        var level = LevelWithWall(Int3.Zero);

        var buildingLayout = new VoxelLayout3D();
        level.PlaceAt(buildingLayout, level.Coords);
        Assert.True(buildingLayout.HasEntity(new Int3(2, 0, 0)));

        level.DestroyAt(buildingLayout, level.Coords);

        Assert.False(buildingLayout.HasEntity(new Int3(2, 0, 0)));
        Assert.False(buildingLayout.HasEntity(new Int3(7, 0, 0)));
        Assert.Empty(buildingLayout.Entities);
    }

    [Fact]
    public void Building_PlaceAt_CopiesAllLevelsIntoParent()
    {
        var building = new Building();
        building.Levels[0] = LevelWithWall(new Int3(0, 0, 0));
        building.Levels[1] = LevelWithWall(new Int3(0, 3, 0));

        var homeLayout = new VoxelLayout3D();
        building.PlaceAt(homeLayout, new Int3(10, 0, 5));

        Assert.True(homeLayout.HasEntity(new Int3(12, 0, 5))); // 10 + 2, 一层
        Assert.True(homeLayout.HasEntity(new Int3(17, 0, 5))); // 10 + 7
        Assert.True(homeLayout.HasEntity(new Int3(12, 3, 5))); // 二层：10 + 2, 3
    }

    [Fact]
    public void Building_Cells_ReportsFullVoxelView()
    {
        var building = new Building();
        building.Levels[0] = LevelWithWall(Int3.Zero);

        var cells = building.Cells3D().ToHashSet();

        Assert.Contains(new Int3(2, 0, 0), cells);
        Assert.Contains(new Int3(7, 0, 0), cells);
    }
}
