using Xunit;
using Momoka.Home.Entities;
using Momoka.Home.Geometry;
using Momoka.Home.Layouts;
using Momoka.Home.Primitives;
namespace Momoka.Home.Tests.Models.Layouts;

/// <summary>
/// Checks the generic chunked voxel storage (VoxelLayout&lt;T&gt; /
/// VoxelChunk&lt;T&gt; / VoxelChunkSection&lt;T&gt;): Minecraft-style XZ chunks
/// with lazy 16×16×16 sections over the height axis, packed-long chunk keys,
/// and the full occupancy API replacing the old VoxelLayout3D role.
/// </summary>
public class VoxelLayoutChunkTests
{
    private sealed class TestEntity : Entity<Int3>
    {
        public TestEntity() => Volume = new Box3D();
    }

    [Fact]
    public void Indexer_WritesAndReadsBackCells()
    {
        var layout = new VoxelLayout<Entity<Int3>>();
        var e = new TestEntity();

        layout[new Int3(3, 5, 7)] = e;

        Assert.Same(e, layout[new Int3(3, 5, 7)]);
        Assert.Null(layout[new Int3(4, 5, 7)]);
    }

    [Fact]
    public void Indexer_NegativeCoordinates_AreIndexedCorrectly()
    {
        var layout = new VoxelLayout<Entity<Int3>>();
        var e = new TestEntity();

        layout[new Int3(-1, 0, -17)] = e;

        Assert.Same(e, layout[new Int3(-1, 0, -17)]);
        Assert.Null(layout[new Int3(0, 0, -17)]);
        Assert.Null(layout[new Int3(-1, 0, -18)]);
    }

    [Fact]
    public void TallWrite_CreatesSectionsAcrossChunks()
    {
        var layout = new VoxelLayout<Entity<Int3>>();
        var low = new TestEntity();
        var high = new TestEntity();

        layout[new Int3(0, 2, 0)] = low;    // section 0
        layout[new Int3(0, 40, 0)] = high;  // section 2 (y 32..47)

        Assert.Same(low, layout[new Int3(0, 2, 0)]);
        Assert.Same(high, layout[new Int3(0, 40, 0)]);
        Assert.Null(layout[new Int3(0, 33, 0)]);
    }

    [Fact]
    public void BuildAt_And_DestroyAt_RegisterAndClear()
    {
        var layout = new VoxelLayout<Entity<Int3>>();
        var e = new TestEntity();

        Assert.True(layout.BuildAt(e, new Int3(2, 0, 2)));
        Assert.Same(e, Assert.Single(layout.Entities));
        Assert.True(layout.HasEntity(new Int3(2, 0, 2)));

        Assert.True(layout.DestroyAt(new Int3(2, 0, 2)));
        Assert.Empty(layout.Entities);
        Assert.False(layout.HasEntity(new Int3(2, 0, 2)));
    }

    [Fact]
    public void BuildAt_Collision_IsRejected()
    {
        var layout = new VoxelLayout<Entity<Int3>>();
        var a = new TestEntity();
        var b = new TestEntity();

        Assert.True(layout.BuildAt(a, new Int3(2, 0, 2)));
        Assert.False(layout.BuildAt(b, new Int3(2, 0, 2)));
        Assert.Single(layout.Entities);
    }

    [Fact]
    public void MergeFrom_And_RemoveFrom_ComposeWithOffset()
    {
        var child = new VoxelLayout<Entity<Int3>>();
        var e = new TestEntity();
        child.BuildAt(e, new Int3(2, 0, 0));

        var parent = new VoxelLayout<Entity<Int3>>();
        parent.MergeFrom(child, new Int3(0, 30, 0));

        Assert.Contains(e, parent.Entities);
        Assert.True(parent.HasEntity(new Int3(2, 30, 0)));

        parent.RemoveFrom(child, new Int3(0, 30, 0));
        Assert.DoesNotContain(e, parent.Entities);
        Assert.False(parent.HasEntity(new Int3(2, 30, 0)));
    }

    [Fact]
    public void Rebuild_RasterizesEntitiesBackIntoChunks()
    {
        var layout = new VoxelLayout<Entity<Int3>>();
        var e = new TestEntity();
        layout.BuildAt(e, new Int3(5, 5, 5));

        layout.Clear();
        Assert.False(layout.HasEntity(new Int3(5, 5, 5)));

        layout.Rebuild();
        Assert.True(layout.HasEntity(new Int3(5, 5, 5)));
    }

    [Fact]
    public void GetEntitiesInBound_And_OfType_Filter()
    {
        var layout = new VoxelLayout<Entity<Int3>>();
        var a = new TestEntity();
        var b = new TestEntity();
        layout.BuildAt(a, new Int3(1, 0, 1));
        layout.BuildAt(b, new Int3(8, 0, 8));

        var inBox = layout.GetEntitiesInBound(new Int2(0, 0), new Int2(3, 3));
        Assert.Equal(new[] { a }, inBox);

        var ofType = layout.GetEntitiesOfType<TestEntity>();
        Assert.Equal(2, ofType.Count);
    }
}
