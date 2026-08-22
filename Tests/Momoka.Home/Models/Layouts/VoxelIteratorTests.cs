using Xunit;
using Momoka.Home.Levels.Layouts;
using Momoka.Home.Primitives;
namespace Momoka.Home.Tests.Models.Layouts;

/// <summary>
/// VoxelIterator&lt;T&gt; is a vertical column cursor: for a fixed XZ it walks
/// every cell from the bound's bottom to its top, yielding an
/// <c>(int Y, T? Value)</c> tuple per cell (default for empty cells), and
/// composes with foreach and LINQ.
/// </summary>
public class VoxelIteratorTests
{
    private static readonly int[] OccupiedYs = { 1, 5, 9 };

    private static VoxelLayout<string> Column()
    {
        var layout = new VoxelLayout<string>
        {
            Bound = Bound.FromCorners(Int3.Zero.ToFloat3(), new Int3(4, 10, 4).ToFloat3()),
            Length = 1f,
        };
        layout[new Int3(2, 1, 2)] = "floor";
        layout[new Int3(2, 5, 2)] = "table";
        layout[new Int3(2, 9, 2)] = "lamp";
        return layout;
    }

    [Fact]
    public void Enumerates_BottomToTop_EveryCell_IncludingEmpty()
    {
        var iterator = new VoxelIterator<string>(Column(), new Int3(2, 0, 2));

        var cells = iterator.ToList();

        Assert.Equal(0, iterator.MinY);
        Assert.Equal(10, iterator.MaxY);
        Assert.Equal(11, cells.Count);
        Assert.Null(cells[0].Value);
        Assert.Equal(1, cells[1].Y);
        Assert.Equal("floor", cells[1].Value);
        Assert.Equal(5, cells[5].Y);
        Assert.Equal("table", cells[5].Value);
        Assert.Equal(9, cells[9].Y);
        Assert.Equal("lamp", cells[9].Value);
        Assert.Null(cells[10].Value);
    }

    [Fact]
    public void Supports_LinqQueries()
    {
        var iterator = new VoxelIterator<string>(Column(), new Int3(2, 0, 2));

        Assert.Equal(3, iterator.Count(cell => cell.Value is not null));
        Assert.Equal(8, iterator.Count(cell => cell.Value is null));
        Assert.Equal("lamp", iterator.Last(cell => cell.Value is not null).Value);
        Assert.Equal(OccupiedYs, iterator
            .Where(cell => cell.Value is not null)
            .Select(cell => cell.Y));
    }

    [Fact]
    public void Foreach_Deconstructs_Y_WithEachCell()
    {
        var layout = Column();
        var y = 0;
        foreach (var (cellY, cell) in new VoxelIterator<string>(layout, new Int3(2, 0, 2)))
        {
            Assert.Equal(y, cellY);
            Assert.Equal(layout[new Int3(2, y, 2)], cell);
            y++;
        }
        Assert.Equal(11, y);
    }

    [Fact]
    public void Int2_And_Int3_Constructors_ProduceTheSameColumn()
    {
        var layout = Column();

        var byPlane = new VoxelIterator<string>(layout, new Int2(2, 2));
        var byCell = new VoxelIterator<string>(layout, new Int3(2, 0, 2));

        Assert.Equal(byPlane.X, byCell.X);
        Assert.Equal(byPlane.Z, byCell.Z);
        Assert.Equal(byPlane.ToList(), byCell.ToList());
    }

    [Fact]
    public void Spans_ChunkSections_AlongTheColumn()
    {
        var layout = new VoxelLayout<string>
        {
            Bound = Bound.FromCorners(Int3.Zero.ToFloat3(), new Int3(4, 40, 4).ToFloat3()),
            Length = 1f,
        };
        layout[new Int3(2, 3, 2)] = "low";
        layout[new Int3(2, 20, 2)] = "mid";   // section 1
        layout[new Int3(2, 37, 2)] = "high";  // section 2

        var cells = new VoxelIterator<string>(layout, new Int3(2, 0, 2)).ToList();

        Assert.Equal(41, cells.Count);
        Assert.Equal(3, cells[3].Y);
        Assert.Equal("low", cells[3].Value);
        Assert.Equal(20, cells[20].Y);
        Assert.Equal("mid", cells[20].Value);
        Assert.Equal(37, cells[37].Y);
        Assert.Equal("high", cells[37].Value);
        Assert.All(cells.Where((cell, i) => i != 3 && i != 20 && i != 37), cell => Assert.Null(cell.Value));
    }

    [Fact]
    public void Empty_WhenBoundIsInvalid()
    {
        var iterator = new VoxelIterator<string>(new VoxelLayout<string>(), new Int3(0, 0, 0));

        Assert.Empty(iterator);
    }
}
