using Xunit;
using Momoka.Home;
using Momoka.Home.Entities;
using Momoka.Home.Layouts;
using Momoka.Home.Primitives;
namespace Momoka.Home.Tests.Models.Layouts;

/// <summary>
/// PlaneLayout = a single-layer placement surface (VoxelLayout2D base) + an
/// embedded material subdivision. No multi-layer stacking.
/// </summary>
public class PlaneLayoutTests
{
    [Fact]
    public void Plane_IsAlsoAPlacementSurface()
    {
        var plane = new PlaneLayout<TileEntity>(new Int2(10, 10));
        plane.Fill(new Int2(0, 0), new Int2(10, 10));

        Assert.False(plane.IsCollided(new Int2(3, 3)));
        Assert.True(plane.IsCollided(new Int2(11, 11))); // 越界
    }

    [Fact]
    public void Plane_EmbedsSubdivisionForMaterialRegions()
    {
        var plane = new PlaneLayout<TileEntity>(new Int2(10, 10));
        plane.Subdivision.AddNode(new Int2(0, 0));
        plane.Subdivision.AddNode(new Int2(4, 0));
        plane.Subdivision.AddNode(new Int2(4, 4));
        plane.Subdivision.AddNode(new Int2(0, 4));
        plane.Subdivision.AddEdge(new Int2(0, 0), new Int2(4, 0));
        plane.Subdivision.AddEdge(new Int2(4, 0), new Int2(4, 4));
        plane.Subdivision.AddEdge(new Int2(4, 4), new Int2(0, 4));
        plane.Subdivision.AddEdge(new Int2(0, 4), new Int2(0, 0));

        Assert.Single(plane.Subdivision.BoundedFaces);
        Assert.NotNull(plane.Subdivision.FaceAt(new Int2(2, 2)));
    }
}
