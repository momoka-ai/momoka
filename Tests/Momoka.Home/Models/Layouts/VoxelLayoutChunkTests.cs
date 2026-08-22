using Xunit;
using Momoka.Home.Levels.Entities;
using Momoka.Home.Levels.Volumes;
using Momoka.Home.Levels.Layouts;
using Momoka.Home.Primitives;
namespace Momoka.Home.Tests.Models.Layouts;

/// <summary>
/// Checks the generic chunked voxel storage (VoxelLayout&lt;T&gt; /
/// VoxelChunk&lt;T&gt; / VoxelChunkSection&lt;T&gt;): Minecraft-style XZ chunks
/// with lazy 16×16×16 sections over the height axis and packed-long chunk keys.
/// Entity placement lives on LevelLayout (tested in UnitLayoutTests).
/// </summary>
public class VoxelLayoutChunkTests
{
    private sealed class TestEntity : Entity
    {
        public TestEntity() => Volume = new Box();
    }

    [Fact]
    public void Indexer_WritesAndReadsBackCells()
    {
        var layout = new VoxelLayout<Entity>();
        var e = new TestEntity();

        layout[new Int3(3, 5, 7)] = e;

        Assert.Same(e, layout[new Int3(3, 5, 7)]);
        Assert.Null(layout[new Int3(4, 5, 7)]);
    }

    [Fact]
    public void Indexer_NegativeCoordinates_AreIndexedCorrectly()
    {
        var layout = new VoxelLayout<Entity>();
        var e = new TestEntity();

        layout[new Int3(-1, 0, -17)] = e;

        Assert.Same(e, layout[new Int3(-1, 0, -17)]);
        Assert.Null(layout[new Int3(0, 0, -17)]);
        Assert.Null(layout[new Int3(-1, 0, -18)]);
    }

    [Fact]
    public void TallWrite_CreatesSectionsAcrossChunks()
    {
        var layout = new VoxelLayout<Entity>();
        var low = new TestEntity();
        var high = new TestEntity();

        layout[new Int3(0, 2, 0)] = low;    // section 0
        layout[new Int3(0, 40, 0)] = high;  // section 2 (y 32..47)

        Assert.Same(low, layout[new Int3(0, 2, 0)]);
        Assert.Same(high, layout[new Int3(0, 40, 0)]);
        Assert.Null(layout[new Int3(0, 33, 0)]);
    }
}
