using Xunit;
using Momoka.Home;
using Momoka.Home.Layouts;
using Momoka.Home.Primitives;
namespace Momoka.Home.Tests.Models.Layouts;

/// <summary>
/// Regression coverage for the chunked paletted storage — including the
/// first-write-lost bug where the palette resize (triggered by the very first
/// write) discarded that write because the receiver of Set(...) was evaluated
/// before the resizing IdFor(...) argument.
/// </summary>
public class PalettedContainerTests
{
    [Fact]
    public void FirstWrite_ThatGrowsPalette_IsNotLost()
    {
        var container = new PalettedContainer<Int2, bool>(
            new Palette<bool>.Int2ChunkStrategy(new Int2(5, 5), 4));

        container[new Int2(1, 1)] = true;

        Assert.True(container[new Int2(1, 1)]);
    }

    [Fact]
    public void GridLayout_WritesAcrossCells_Persist()
    {
        var grid = new GridLayout<bool>(new Int2(5, 5));

        grid[new Int2(1, 1)] = true;
        grid[new Int2(2, 2)] = true;
        grid[new Int2(3, 3)] = true;

        Assert.True(grid[new Int2(1, 1)]);
        Assert.True(grid[new Int2(2, 2)]);
        Assert.True(grid[new Int2(3, 3)]);
    }

    [Fact]
    public void GridLayout_UnsetCell_DefaultsToFalse()
    {
        var grid = new GridLayout<bool>(new Int2(5, 5));
        Assert.False(grid[new Int2(0, 0)]);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(2, 4)]
    [InlineData(7, -3)]
    [InlineData(-3, 7)]
    [InlineData(-4, -5)]
    public void GridLayout_WriteRead_RoundTrips(int x, int z)
    {
        var grid = new GridLayout<bool>(new Int2(4, 4));
        grid[new Int2(x, z)] = true;
        Assert.True(grid[new Int2(x, z)]);
    }
}
