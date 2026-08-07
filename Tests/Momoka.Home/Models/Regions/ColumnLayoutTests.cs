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
}
