using Xunit;
using Momoka.Home;
using Momoka.Home.Levels.Layouts;
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
}
