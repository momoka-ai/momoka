using Xunit;
using Momoka.Home.Entities;
using Momoka.Home.Geometry;
using Momoka.Home.Layouts;
using Momoka.Home.Primitives;
namespace Momoka.Home.Tests.Models.Layouts;

/// <summary>
/// VoxelLayout&lt;T&gt; is a pure chunked 3D grid: the indexer reads/writes
/// values, Select maps them into a new grid, Clear resets storage. No entity or
/// placement semantics — those live on UnitLayout (see UnitLayoutTests).
/// </summary>
public class VoxelLayoutTests
{
    private sealed class TestEntity : Entity
    {
        public TestEntity(Volume volume) => Volume = volume;
    }

    [Fact]
    public void Select_MapsEveryOccupiedCell_AndCopiesBound()
    {
        var layout = new VoxelLayout<Entity>
        {
            Bound = Bound.FromCorners(Int3.Zero, new Int3(7, 7, 7)),
        };
        var a = new TestEntity(new Box3D { SizeX = 2, SizeY = 1, SizeZ = 2 });
        var b = new TestEntity(new Box3D { SizeX = 1, SizeY = 1, SizeZ = 1 });
        layout[new Int3(1, 0, 1)] = a;
        layout[new Int3(2, 0, 1)] = a;
        layout[new Int3(1, 0, 2)] = a;
        layout[new Int3(2, 0, 2)] = a;
        layout[new Int3(5, 0, 5)] = b;

        var mapped = layout.Select(_ => true);

        Assert.Equal(layout.Bound.Min, mapped.Bound.Min);
        Assert.Equal(layout.Bound.Max, mapped.Bound.Max);
        Assert.True(mapped[new Int3(1, 0, 1)]);
        Assert.True(mapped[new Int3(5, 0, 5)]);
        Assert.False(mapped[new Int3(0, 0, 0)]); // 空格未映射
        Assert.False(mapped[new Int3(3, 0, 3)]);
    }

    [Fact]
    public void Select_SkipsDefaultMappedValues()
    {
        var layout = new VoxelLayout<Entity>();
        layout[new Int3(1, 0, 1)] = new TestEntity(new Box3D());
        layout[new Int3(4, 0, 4)] = new TestEntity(new Box3D());

        var mapped = layout.Select(_ => false);

        Assert.False(mapped[new Int3(1, 0, 1)]); // 映射为 false 的格不存储，读默认
        Assert.False(mapped[new Int3(0, 0, 0)]);
    }

    [Fact]
    public void Clear_RemovesAllCells()
    {
        var layout = new VoxelLayout<Entity>();
        var e = new TestEntity(new Box3D());
        layout[new Int3(3, 5, 7)] = e;

        Assert.Same(e, layout[new Int3(3, 5, 7)]);
        layout.Clear();
        Assert.Null(layout[new Int3(3, 5, 7)]);
    }

    [Fact]
    public void GetEntityAtPoint_And_GetEntityAtNearest_FindValues()
    {
        var layout = new VoxelLayout<Entity>();
        var a = new TestEntity(new Box3D());
        layout[new Int3(3, 5, 7)] = a;

        Assert.Same(a, layout.GetEntityAtPoint(new Int3(3, 5, 7)));
        Assert.Same(a, layout.GetEntityAtNearest(new Int3(3, 5, 7)));
        Assert.Same(a, layout.GetEntityAtNearest(new Int3(4, 5, 7))); // 相邻格
        Assert.Null(new VoxelLayout<Entity>().GetEntityAtNearest(new Int3(0, 0, 0))); // 空布局
    }
}
