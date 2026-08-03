using Xunit;
using Momoka.Home;
using Momoka.Home.Entities;
using Momoka.Home.Layouts;
using Momoka.Home.Levels;
using Momoka.Home.Primitives;
using Momoka.Home.Shapes;
namespace Momoka.Home.Tests.Models.Levels;

/// <summary>
/// End-to-end checks of the level as the unified placement-surface catalog:
/// Level : IVoxelLayout2DSource aggregates its floor/ceiling planes and every
/// contained surface-source entity's surfaces (walls' faces and any other
/// IVoxelLayout2DSource placed in the occupancy grid).
/// </summary>
public class LevelTests
{
    private sealed class SurfaceSourceEntity : VoxelEntity, IVoxelLayout2DSource
    {
        public SurfaceSourceEntity()
        {
            Shape = new BoxShape();
            Surface = new VoxelLayout2D(new Int2(2, 2));
        }

        public VoxelLayout2D Surface { get; }

        public IEnumerable<VoxelLayout2D> Layouts => new[] { Surface };
    }
    [Fact]
    public void Layouts_IncludesFloorCeilingAndWallFaces()
    {
        var level = new Level();
        var wall = new Wall();
        level.Plan.Build(new Int2(2, 0), new Int2(7, 0), wall);
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
    public void Layouts_IncludesAnySurfaceSourceEntity_NotJustWalls()
    {
        var level = new Level();
        var source = new SurfaceSourceEntity();
        level.Layout.BuildAt(source, new Int3(2, 0, 0));

        // floor + ceiling + the custom surface-source entity's surface
        Assert.Equal(3, level.Layouts.Count());
        Assert.Contains(source.Surface, level.Layouts);
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
