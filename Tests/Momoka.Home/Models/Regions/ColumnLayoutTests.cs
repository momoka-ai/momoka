using Xunit;
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
    public void Build_PacksVariableSpansPerColumn()
    {
        // 墙列 x=1 空；x=2 夹层双 span → 变长列 + 前缀和
        var scene = Scene(3, 1, 30,
            (new Int3(1, 0, 0), new Int3(1, 31, 1)),  // x=1 全高墙
            (new Int3(2, 20, 0), new Int3(1, 1, 1))); // x=2 夹层体 y=20
        var labels = ColumnLayout<int>.Build(scene,
            new List<Int3> { new(0, 1, 0), new(2, 1, 0), new(2, 21, 0) },
            new ColumnLayout<int>.Settings());

        Assert.Equal(3, labels.Width);
        Assert.Equal(1, labels.Depth);
        Assert.Equal(3, labels.SpanCount);
        Assert.Equal(1, labels.Column(0, 0).Length);
        Assert.True(labels.Column(1, 0).IsEmpty); // 墙列
        Assert.Equal(2, labels.Column(2, 0).Length); // 夹层双 span

        Assert.Equal(1, labels.At(0, 5, 0));   // 左区
        Assert.Equal(2, labels.At(2, 5, 0));   // 夹层下 [1,20)
        Assert.Equal(3, labels.At(2, 25, 0));  // 夹层上 [21,31)
    }

    [Fact]
    public void At_OutsideFootprint_ReturnsDefault()
    {
        var scene = Scene(2, 1, 30);
        var labels = ColumnLayout<int>.Build(scene,
            new List<Int3> { new(0, 1, 0) },
            new ColumnLayout<int>.Settings());

        Assert.Equal(0, labels.At(-1, 5, 0));
        Assert.Equal(0, labels.At(5, 5, 5));
        Assert.Equal(0, labels.At(0, 5, -3));
    }

    [Fact]
    public void AllSpans_EnumeratesEverySpanWithColumn()
    {
        // 中墙 x=1 隔开两列；两列各有夹层 → 4 个 span，列主序枚举
        var scene = Scene(3, 1, 30,
            (new Int3(1, 0, 0), new Int3(1, 31, 1)),  // 中墙 x=1
            (new Int3(0, 20, 0), new Int3(1, 1, 1)),  // 列(0,0) 夹层 y=20
            (new Int3(2, 10, 0), new Int3(1, 1, 1))); // 列(2,0) 平台 y=10
        var labels = ColumnLayout<int>.Build(scene,
            new List<Int3> { new(0, 1, 0), new(0, 21, 0), new(2, 1, 0), new(2, 11, 0) },
            new ColumnLayout<int>.Settings());

        var spans = labels.AllSpans().ToList();
        Assert.Equal(4, spans.Count);
        Assert.Equal((0, 0, 1), (spans[0].X, spans[0].Z, spans[0].Span.Value));
        Assert.Equal((0, 0, 2), (spans[1].X, spans[1].Z, spans[1].Span.Value));
        Assert.Equal((2, 0, 3), (spans[2].X, spans[2].Z, spans[2].Span.Value));
        Assert.Equal((2, 0, 4), (spans[3].X, spans[3].Z, spans[3].Span.Value));
    }

    [Fact]
    public void Span_EqualsByYAndValue()
    {
        var a = new ColumnLayout<int>.Span(1, 5, 7);
        var b = new ColumnLayout<int>.Span(1, 5, 7);
        var c = new ColumnLayout<int>.Span(1, 6, 7);

        Assert.Equal(a, b);
        Assert.True(a == b);
        Assert.False(a == c);
        Assert.True(a != c);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }

    // ── 生成引擎 Build（站立格 + 占用）─────────────────

    /// <summary>一个带 Bound 的占用网格；true = 被占用（阻塞），按 (位置, 尺寸) 写入。</summary>
    private static VoxelLayout<bool> Scene(int width, int depth, int height, params (Int3 Pos, Int3 Size)[] blocks)
    {
        var layout = new VoxelLayout<bool>
        {
            Bound = Bound.FromCorners(Int3.Zero, new Int3(width - 1, height - 1, depth - 1)),
        };
        foreach (var (pos, size) in blocks)
        {
            for (var x = 0; x < size.X; x++)
                for (var y = 0; y < size.Y; y++)
                    for (var z = 0; z < size.Z; z++)
                        layout[pos + new Int3(x, y, z)] = true;
        }
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
        Assert.True(labels.Column(2, 0).IsEmpty); // 墙列空
        Assert.Equal(0, labels.At(-1, 5, 0)); // footprint 外
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
            new VoxelLayout<bool>(),
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
        // 引擎建左右两区，Map 把 label 重映射为 payload
        var scene = Scene(5, 1, 30, (new Int3(2, 0, 0), new Int3(1, 31, 1))); // x=2 全高墙
        var labels = ColumnLayout<int>.Build(scene,
            new List<Int3> { new(0, 1, 0), new(3, 1, 0) },
            new ColumnLayout<int>.Settings());
        var mapped = labels.Map(v => $"R{v}");

        Assert.Equal("R1", mapped.At(0, 5, 0));
        Assert.Equal("R2", mapped.At(3, 5, 0));
    }
}
