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
        Assert.Equal(18, h.Height);
        Assert.Equal(4, h.Radius);
        Assert.Equal(2, h.MaxClimbHeight);
        Assert.Equal(6, h.MaxJumpHeight);
        Assert.Equal(30, h.MaxWalkLength);
    }

    [Fact]
    public void Custom_OverridesDefaults()
    {
        var robot = new Agent(Height: 12, Radius: 3, MaxClimbHeight: 1, MaxJumpHeight: 4, MaxWalkLength: 20);
        Assert.Equal(12, robot.Height);
        Assert.Equal(3, robot.Radius);
        Assert.Equal(1, robot.MaxClimbHeight);
        Assert.Equal(4, robot.MaxJumpHeight);
        Assert.Equal(20, robot.MaxWalkLength);
    }
}
