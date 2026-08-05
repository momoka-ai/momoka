using BenchmarkDotNet.Attributes;
using Momoka.Home;
using Momoka.Home.Layouts;
using Momoka.Home.Primitives;
namespace Momoka.Home.Benchmarks;

/// <summary>
/// Throughput of the chunked paletted container — the hot path behind grid
/// writes/reads (Level occupancy, VoxelLayout2D placement cells).
/// Run with: dotnet run -c Release --project Benchmarks/Momoka.Home
/// </summary>
[MemoryDiagnoser]
public class PalettedContainerBenchmarks
{
    private readonly PalettedContainer<Int2, bool> _container =
        new(new Palette<bool>.Int2ChunkStrategy(new Int2(20, 20), 4));

    private static readonly Int2[] Cells = BuildCells();

    private static Int2[] BuildCells()
    {
        var cells = new Int2[400];
        var i = 0;
        for (var x = 0; x < 20; x++)
        {
            for (var z = 0; z < 20; z++)
            {
                cells[i++] = new Int2(x, z);
            }
        }
        return cells;
    }

    [Benchmark]
    public void WriteAllCells()
    {
        foreach (var cell in Cells)
        {
            _container[cell] = true;
        }
    }

    [Benchmark]
    public void ReadAllCells()
    {
        foreach (var cell in Cells)
        {
            _ = _container[cell];
        }
    }
}
