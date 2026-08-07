using Xunit;
using Momoka.Home.Layouts;
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

    // ── 生成引擎 Build ──────────────────────────────────

    private static HashSet<(int X, int Y, int Z)> WallBlock(
        int width, int depth, int minY, int maxY, Func<int, int, bool> blockedXz)
    {
        var cells = new HashSet<(int, int, int)>();
        for (var z = 0; z < depth; z++)
        for (var y = minY; y <= maxY; y++)
        for (var x = 0; x < width; x++)
            if (blockedXz(x, z))
                cells.Add((x, y, z));
        return cells;
    }

    [Fact]
    public void Build_LabelsConnectedSpans()
    {
        // 5×5，x=2 全高墙 → 左右两个连通分量
        var blocked = WallBlock(5, 5, 0, 10, (x, z) => x == 2);
        var layout = ColumnLayout<int>.Build(5, 5, 0, 10,
            isFree: (x, y, z) => !blocked.Contains((x, y, z)),
            linked: (a, b) => true,
            valueOf: id => id);

        Assert.Equal(20, layout.SpanCount); // 4 自由列 × 5 深，每列 1 个 span
        var left = layout.At(0, 5, 0);
        var right = layout.At(3, 5, 0);
        Assert.NotEqual(0, left);
        Assert.NotEqual(0, right);
        Assert.NotEqual(left, right);
        Assert.Equal(0, layout.At(2, 5, 0)); // 墙
    }

    [Fact]
    public void Build_MergesThroughGap()
    {
        // x=2 墙留 z=2 缺口 → 单连通
        var blocked = WallBlock(5, 5, 0, 10, (x, z) => x == 2 && z != 2);
        var layout = ColumnLayout<int>.Build(5, 5, 0, 10,
            isFree: (x, y, z) => !blocked.Contains((x, y, z)),
            linked: (a, b) => true,
            valueOf: id => id);

        var left = layout.At(0, 5, 0);
        Assert.NotEqual(0, left);
        Assert.Equal(left, layout.At(3, 5, 0)); // 门洞连通
    }

    [Fact]
    public void Build_LinkedRuleControlsConnectivity()
    {
        // 两列：A 仅自由 [1,2)，B 仅自由 [4,5)，间距 2
        var blocked = new HashSet<(int, int, int)>
        {
            (0, 0, 0), (0, 2, 0), (0, 3, 0), (0, 4, 0), (0, 5, 0), // 列 0
            (1, 0, 0), (1, 1, 0), (1, 2, 0), (1, 3, 0), (1, 5, 0), // 列 1
        };

        // 容差 2 内 → 连通
        var merged = ColumnLayout<int>.Build(2, 1, 0, 5,
            isFree: (x, y, z) => !blocked.Contains((x, y, z)),
            linked: (a, b) => Math.Max(a.Y0, b.Y0) - Math.Min(a.Y1, b.Y1) <= 2,
            valueOf: id => id);
        Assert.Equal(merged.At(0, 1, 0), merged.At(1, 4, 0));

        // 容差 1 → 断开
        var split = ColumnLayout<int>.Build(2, 1, 0, 5,
            isFree: (x, y, z) => !blocked.Contains((x, y, z)),
            linked: (a, b) => Math.Max(a.Y0, b.Y0) - Math.Min(a.Y1, b.Y1) <= 1,
            valueOf: id => id);
        Assert.NotEqual(split.At(0, 1, 0), split.At(1, 4, 0));
    }

    [Fact]
    public void Build_AllBlocked_HasNoSpans()
    {
        var layout = ColumnLayout<int>.Build(3, 3, 0, 5,
            isFree: (_, _, _) => false,
            linked: (_, _) => true,
            valueOf: id => id);

        Assert.Equal(0, layout.SpanCount);
        Assert.Equal(0, layout.At(1, 2, 1));
    }

    [Fact]
    public void Build_ValueOfMaterializesPayload()
    {
        var blocked = WallBlock(5, 5, 0, 10, (x, z) => x == 2);
        var layout = ColumnLayout<string>.Build(5, 5, 0, 10,
            isFree: (x, y, z) => !blocked.Contains((x, y, z)),
            linked: (a, b) => true,
            valueOf: id => $"R{id}");

        Assert.Equal("R1", layout.At(0, 5, 0));
        Assert.Equal("R2", layout.At(3, 5, 0));
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
