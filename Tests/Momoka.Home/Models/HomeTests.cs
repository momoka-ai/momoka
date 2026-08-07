using Xunit;
using Momoka.Home.Entities;
using Momoka.Home.Layouts;
using Momoka.Home.Primitives;
namespace Momoka.Home.Tests.Models;

/// <summary>
/// The site root: a spatial container whose ground is a placement surface
/// (PlaneLayout) with an embedded material subdivision, like every floor plane.
/// </summary>
public class HomeTests
{
    [Fact]
    public void Ground_IsPlaneLayoutWithSubdivisionFacingUp()
    {
        var home = new Home();

        var ground = Assert.IsType<PlaneLayout<Entity>>(home.Ground);
        Assert.NotNull(ground.Subdivision);
        Assert.Equal(Int3.Up, ground.Direction);
    }
}
