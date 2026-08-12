using Xunit;
using Momoka.Home;
namespace Momoka.Home.Tests.Models;

/// <summary>
/// Agent carries the movement attributes a mobile unit needs for region
/// connectivity and pathfinding (cells of 10 cm; defaults are human).
/// </summary>
public class AgentTests
{
    [Fact]
    public void Human_Defaults()
    {
        var h = Agent.Human;
        Assert.Equal(180, h.Height);
        Assert.Equal(40, h.Radius);
        Assert.Equal(20, h.MaxClimbHeight);
        Assert.Equal(60, h.MaxJumpHeight);
        Assert.Equal(80, h.MaxWalkLength);
        Assert.Equal(60, h.MaxInteractLength);
    }

    [Fact]
    public void Custom_OverridesDefaults()
    {
        var robot = new Agent(Height: 120, Radius: 30, MaxClimbHeight: 10, MaxJumpHeight: 40, MaxWalkLength: 200, MaxInteractLength: 60);
        Assert.Equal(120, robot.Height);
        Assert.Equal(30, robot.Radius);
        Assert.Equal(10, robot.MaxClimbHeight);
        Assert.Equal(40, robot.MaxJumpHeight);
        Assert.Equal(200, robot.MaxWalkLength);
        Assert.Equal(60, robot.MaxInteractLength);
    }
}
