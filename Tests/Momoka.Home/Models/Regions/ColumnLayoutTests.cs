using Xunit;
using Momoka.Home.Levels.Layouts;
using Momoka.Home.Primitives;
namespace Momoka.Home.Tests.Models.Regions;

/// <summary>
/// ColumnLayout wraps a <see cref="VoxelLayout{T}"/> and enforces column-span
/// writes: values are set as whole Y-intervals (<see cref="ColumnLayout{T}.SetSpan"/>)
/// or auto-extended to blockers (<see cref="ColumnLayout{T}.SetAt"/>), so a
/// column's runs stay homogeneous.
/// </summary>
public class ColumnLayoutTests
{
    private static ColumnLayout<int> Layout(params Int3[] blocked) => new(p => blocked.Contains(p));

    [Fact]
    public void SetSpan_WritesRun_AtReads()
    {
        var layout = Layout();
        layout.SetSpan(2, 3, 8, 5, 42);

        Assert.Equal(42, layout.At(2, 3, 5));
        Assert.Equal(42, layout.At(2, 7, 5));
        Assert.Equal(0, layout.At(2, 2, 5));
        Assert.Equal(0, layout.At(2, 8, 5));
        Assert.Equal(0, layout.At(1, 5, 5)); // 相邻列不受影响
    }

    [Fact]
    public void SetSpan_Overwrite_TruncatesNeighbors()
    {
        var layout = Layout();
        layout.SetSpan(0, 0, 10, 0, 1);
        layout.SetSpan(0, 4, 6, 0, 2);

        Assert.Equal(1, layout.At(0, 3, 0));
        Assert.Equal(2, layout.At(0, 4, 0));
        Assert.Equal(2, layout.At(0, 5, 0));
        Assert.Equal(1, layout.At(0, 6, 0));
    }

    [Fact]
    public void SetAt_ExtendsToBlockers()
    {
        var layout = Layout(new Int3(1, 0, 1), new Int3(1, 10, 1));
        layout.Bound = Bound.FromCorners(Int3.Zero.ToFloat3(), new Int3(2, 10, 2).ToFloat3());
        layout.SetAt(1, 5, 1, 7);

        Assert.Equal(7, layout.At(1, 1, 1));
        Assert.Equal(7, layout.At(1, 9, 1));
        Assert.Equal(0, layout.At(1, 0, 1));  // 地板阻挡
        Assert.Equal(0, layout.At(1, 10, 1)); // 天花板阻挡
    }

    [Fact]
    public void SetAt_WithoutBound_OnlyExtendsToBlockers()
    {
        var layout = Layout(new Int3(0, 0, 0));
        layout.SetAt(0, 3, 0, 5);

        Assert.Equal(5, layout.At(0, 1, 0));
        Assert.Equal(5, layout.At(0, 3, 0));
        Assert.Equal(0, layout.At(0, 0, 0));
        Assert.Equal(0, layout.At(0, 4, 0)); // 无 Bound → 不向上延伸
    }

    [Fact]
    public void Cells_EnumeratesAllOccupied()
    {
        var layout = Layout();
        layout.SetSpan(1, 2, 4, 3, 9);

        // int 不可空：Cells() 枚举整块非空 int，用值过滤出实际占用的 2 格。
        var cells = layout.Cells().Where(c => c.Value != 0).ToList();
        Assert.Equal(2, cells.Count);
        Assert.All(cells, c => Assert.Equal(9, c.Value));
        Assert.Equal(new Int3(1, 2, 3), cells[0].Position);
        Assert.Equal(new Int3(1, 3, 3), cells[1].Position);
    }
}
