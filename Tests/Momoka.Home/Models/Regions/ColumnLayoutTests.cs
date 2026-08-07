using Xunit;
using Momoka.Home.Entities;
using Momoka.Home.Geometry;
using Momoka.Home.Layouts;
using Momoka.Home.Primitives;
namespace Momoka.Home.Tests.Models.Regions;

/// <summary>
/// ColumnLayout packs variable-length per-column Y-interval lists into a flat
/// contiguous span array + prefix-sum offsets: columns keep a fixed slot
/// (z*Width + x) while the span count per column varies freely.
/// </summary>
public class ColumnLayoutTests
{
    [Fact]
    public void Pack_StoresVariableSpansPerColumn()
    {
        // 3×3 足迹；c1 两个区间，c3 空列
        var b = new ColumnLayout<int>.Builder(3, 3);
        b.AddSpan(1, 59, 10); b.NextColumn();
        b.AddSpan(1, 29, 20); b.AddSpan(31, 59, 21); b.NextColumn();
        b.AddSpan(1, 59, 30); b.NextColumn();
        b.NextColumn(); // c3 空
        b.AddSpan(1, 59, 40); b.NextColumn();
        b.AddSpan(1, 19, 50); b.AddSpan(21, 59, 51); b.NextColumn();
        b.AddSpan(1, 59, 60); b.NextColumn();
        b.AddSpan(1, 59, 70); b.NextColumn();
        b.AddSpan(1, 59, 80); b.NextColumn();
        var layout = b.Build();

        Assert.Equal(3, layout.Width);
        Assert.Equal(3, layout.Depth);
        Assert.Equal(10, layout.SpanCount);

        Assert.Equal(1, layout.Column(0, 0).Length);
        Assert.Equal(2, layout.Column(1, 0).Length);
        Assert.True(layout.Column(0, 1).IsEmpty); // 空列
        Assert.Equal(1, layout.Column(2, 2).Length);

        Assert.Equal(10, layout.At(0, 5, 0));
        Assert.Equal(20, layout.At(1, 10, 0));
        Assert.Equal(21, layout.At(1, 40, 0));
        Assert.Equal(0, layout.At(1, 30, 0));  // 区间空隙
        Assert.Equal(0, layout.At(0, 1, 1));   // 空列
    }

    [Fact]
    public void At_OutsideFootprint_ReturnsDefault()
    {
        var layout = new ColumnLayout<int>.Builder(2, 2).Build();
        Assert.Equal(0, layout.At(-1, 0, 0));
        Assert.Equal(0, layout.At(5, 0, 5));
        Assert.Equal(0, layout.At(0, 0, -3));
    }

    [Fact]
    public void Build_PadsMissingTrailingColumns()
    {
        var b = new ColumnLayout<string>.Builder(2, 1);
        b.AddSpan(1, 3, "a"); b.NextColumn();
        // 第二列不喂，Build 自动补齐
        var layout = b.Build();

        Assert.Equal(2, layout.ColumnCount);
        Assert.Equal(1, layout.Column(0, 0).Length);
        Assert.True(layout.Column(1, 0).IsEmpty);
        Assert.Equal("a", layout.At(0, 2, 0));
        Assert.Null(layout.At(1, 2, 0));
    }

    [Fact]
    public void Builder_RejectsOverlappingSpansInColumn()
    {
        var b = new ColumnLayout<int>.Builder(1, 1);
        b.AddSpan(1, 5, 1);
        Assert.Throws<InvalidOperationException>(() => b.AddSpan(3, 7, 2));
    }

    [Fact]
    public void Builder_RejectsEmptySpan()
    {
        var b = new ColumnLayout<int>.Builder(1, 1);
        Assert.Throws<ArgumentOutOfRangeException>(() => b.AddSpan(5, 5, 1));
    }

    [Fact]
    public void AllSpans_EnumeratesEverySpanWithColumn()
    {
        var b = new ColumnLayout<int>.Builder(2, 2);
        b.AddSpan(1, 3, 1); b.NextColumn();
        b.NextColumn();
        b.AddSpan(4, 6, 2); b.NextColumn();
        b.NextColumn();
        var layout = b.Build();

        var spans = layout.AllSpans().ToList();
        Assert.Equal(2, spans.Count);
        Assert.Equal((0, 0, 1), (spans[0].X, spans[0].Z, spans[0].Span.Value));
        Assert.Equal((0, 1, 2), (spans[1].X, spans[1].Z, spans[1].Span.Value));
    }

    // ── 生成引擎 Build（站立格 + 占用）─────────────────

    private sealed class TestEntity : Entity
    {
        public TestEntity(Volume volume) => Volume = volume;
    }

    /// <summary>一个带 Bound 的布局；占用块按 (位置, 尺寸) 放入。</summary>
    private static VoxelLayout<Entity> Scene(int width, int depth, int height, params (Int3 Pos, Int3 Size)[] blocks)
    {
        var layout = new VoxelLayout<Entity>
        {
            Bound = Bound.FromCorners(Int3.Zero, new Int3(width - 1, height - 1, depth - 1)),
        };
        foreach (var (pos, size) in blocks)
            layout.BuildAt(new TestEntity(new Box3D { SizeX = size.X, SizeY = size.Y, SizeZ = size.Z }), pos);
        return layout;
    }

    private static IEnumerable<Int3> FloorCells(int width, int depth, int y)
    {
        for (var z = 0; z < depth; z++)
        for (var x = 0; x < width; x++)
            yield return new Int3(x, y, z);
    }

    [Fact]
    public void Build_SplitsByWall()
    {
        // 5×5×11，x=2 全高墙 → 左右两个连通分量
        var layout = Scene(5, 5, 11, (new Int3(2, 0, 0), new Int3(1, 11, 5)));
        var labels = ColumnLayout<int>.Build(layout, FloorCells(5, 5, 1), new ColumnLayout<int>.Settings());

        var left = labels.At(0, 5, 0);
        var right = labels.At(3, 5, 0);
        Assert.NotEqual(0, left);
        Assert.NotEqual(0, right);
        Assert.NotEqual(left, right);
        Assert.Equal(0, labels.At(2, 5, 2)); // 墙列：站立格被占用 → 无 span
    }

    [Fact]
    public void Build_MergesThroughDoorway()
    {
        // x=2 墙留 z=2 门洞 → 单连通
        var layout = Scene(5, 5, 11,
            (new Int3(2, 0, 0), new Int3(1, 11, 2)),
            (new Int3(2, 0, 3), new Int3(1, 11, 2)));
        var labels = ColumnLayout<int>.Build(layout, FloorCells(5, 5, 1), new ColumnLayout<int>.Settings());

        var left = labels.At(0, 5, 0);
        Assert.NotEqual(0, left);
        Assert.Equal(left, labels.At(3, 5, 0)); // 门洞连通
    }

    [Fact]
    public void Build_SettingsClimbHeightControlsConnectivity()
    {
        // 两列：A 站立 y=1（span [1,20)），B 站立 y=21（span [21,40)），间距 1
        var layout = Scene(2, 1, 40,
            (new Int3(0, 0, 0), new Int3(1, 1, 1)),   // A 地板 y=0
            (new Int3(0, 20, 0), new Int3(1, 1, 1)),  // A 天花板 y=20
            (new Int3(1, 0, 0), new Int3(1, 20, 1)),  // B 基座 y=0..19
            (new Int3(1, 20, 0), new Int3(1, 1, 1)),  // B 地板 y=20
            (new Int3(1, 40, 0), new Int3(1, 1, 1))); // B 天花板 y=40
        var cells = new List<Int3> { new(0, 1, 0), new(1, 21, 0) };

        var merged = ColumnLayout<int>.Build(layout, cells, new ColumnLayout<int>.Settings { MaxClimbHeight = 1 });
        Assert.Equal(merged.At(0, 5, 0), merged.At(1, 25, 0)); // 间距 1 ≤ 容差 1 → 连通

        var split = ColumnLayout<int>.Build(layout, cells, new ColumnLayout<int>.Settings { MaxClimbHeight = 0 });
        Assert.NotEqual(split.At(0, 5, 0), split.At(1, 25, 0)); // 超出 → 断开
    }

    [Fact]
    public void Build_SpanStopsAtNextLevel()
    {
        // 夹层：同一列 floor y=1 与 deck 面 y=21（deck 体 y=20 占用）→ 两个 span
        var layout = Scene(1, 1, 30, (new Int3(0, 20, 0), new Int3(1, 1, 1)));
        var cells = new List<Int3> { new(0, 1, 0), new(0, 21, 0) };

        var labels = ColumnLayout<int>.Build(layout, cells, new ColumnLayout<int>.Settings());

        Assert.Equal(2, labels.Column(0, 0).Length);
        Assert.Equal(1, labels.At(0, 5, 0));   // [1,20)
        Assert.Equal(2, labels.At(0, 25, 0));  // [21,31)
        Assert.NotEqual(labels.At(0, 5, 0), labels.At(0, 25, 0));
    }

    [Fact]
    public void Build_NoBound_ReturnsEmpty()
    {
        var labels = ColumnLayout<int>.Build(
            new VoxelLayout<Entity>(),
            new[] { new Int3(0, 1, 0) },
            new ColumnLayout<int>.Settings());
        Assert.Equal(0, labels.SpanCount);
    }

    [Fact]
    public void Build_NoCells_ReturnsEmpty()
    {
        var layout = Scene(3, 3, 5);
        var labels = ColumnLayout<int>.Build(layout, Array.Empty<Int3>(), new ColumnLayout<int>.Settings());
        Assert.Equal(0, labels.SpanCount);
    }

    [Fact]
    public void Map_RemapsSpanValues()
    {
        var b = new ColumnLayout<int>.Builder(2, 1);
        b.AddSpan(1, 5, 7); b.NextColumn();
        b.AddSpan(2, 4, 8); b.NextColumn();
        var mapped = b.Build().Map(v => $"v{v}");

        Assert.Equal("v7", mapped.At(0, 2, 0));
        Assert.Equal("v8", mapped.At(1, 3, 0));
    }
}
