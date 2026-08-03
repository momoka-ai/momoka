using Momoka.Home.Models;
using Momoka.Home.Models.Layouts;
using Momoka.Home.Primitives;
using Xunit;

namespace Momoka.Home.Tests.Models.Layouts;

/// <summary>
/// PlaneLayout = placement surface (VoxelLayout2D base) + embedded material
/// subdivision + attachment layers along the plane's normal.
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

    [Fact]
    public void AddLayer_PlacesSurfaceAlongDirection()
    {
        var plane = new PlaneLayout<TileEntity>(new Int2(10, 10), new Int3(0, 0, 0)) { Direction = Int3.Up };
        var layer = plane.AddLayer(2);

        Assert.Equal(2, layer.Height);
        Assert.Equal(new Int3(0, 2, 0), layer.Surface.Offset);
        Assert.Equal(Int3.Up, layer.Surface.Direction);
        Assert.Same(layer, plane.LayerAt(2));
    }

    [Fact]
    public void AddLayer_OnDownFacingCeiling_ExtendsBelow()
    {
        var ceiling = new PlaneLayout<TileEntity>(new Int2(10, 10), new Int3(0, 3, 0)) { Direction = Int3.Down };
        var layer = ceiling.AddLayer(1);

        Assert.Equal(new Int3(0, 2, 0), layer.Surface.Offset); // 天花板下方 1 格
    }

    [Fact]
    public void AddLayer_DuplicateOrNonPositive_Throws()
    {
        var plane = new PlaneLayout<TileEntity>(new Int2(10, 10));
        plane.AddLayer(2);

        Assert.Throws<ArgumentException>(() => plane.AddLayer(2));
        Assert.Throws<ArgumentOutOfRangeException>(() => plane.AddLayer(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => plane.AddLayer(-1));
    }

    [Fact]
    public void RemoveLayer_RemovesOnlyTheRequestedHeight()
    {
        var plane = new PlaneLayout<TileEntity>(new Int2(10, 10));
        plane.AddLayer(1);
        plane.AddLayer(3);

        Assert.True(plane.RemoveLayer(1));
        Assert.Null(plane.LayerAt(1));
        Assert.NotNull(plane.LayerAt(3));
        Assert.False(plane.RemoveLayer(5));
    }

    [Fact]
    public void Layouts_IncludesPlaneAndLayers_OrderedByHeight()
    {
        var plane = new PlaneLayout<TileEntity>(new Int2(10, 10));
        plane.AddLayer(3);
        plane.AddLayer(1);

        Assert.Equal(3, plane.Layouts.Count());
        Assert.Same(plane, plane.Layouts.ElementAt(0));
        Assert.Equal(1, plane.Layouts.ElementAt(1).Offset.Y);
        Assert.Equal(3, plane.Layouts.ElementAt(2).Offset.Y);
    }
}
