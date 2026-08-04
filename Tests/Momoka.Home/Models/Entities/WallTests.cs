using Xunit;
using Momoka.Home;
using Momoka.Home.Entities;
using Momoka.Home.Layouts;
using Momoka.Home.Primitives;
using Momoka.Home.Shapes;
namespace Momoka.Home.Tests.Models.Entities;

/// <summary>
/// A wall exposes its two faces as placement surfaces (via its VoxelLayoutSource
/// component, refreshed on Layouts access), axis-aligned only. Face geometry is
/// driven by the LineShape (local) + Coords + Height.
/// </summary>
public class WallTests
{
    [Fact]
    public void EastWestWall_ExposesSouthAndNorthFaces()
    {
        var wall = new Wall { Height = 3 };
        wall.Coords = new Int3(2, 0, 0);
        var line = (LineShape)wall.Shape;
        line.Start = Float3.Zero;
        line.End = new Float3(5, 0, 0); // 东西走向，长 5

        var faces = wall.Layouts;

        Assert.Equal(2, faces.Count());
        Assert.Contains(faces, f => f.Direction == Int3.South);
        Assert.Contains(faces, f => f.Direction == Int3.North);

        var south = faces.First(f => f.Direction == Int3.South);
        Assert.Equal(new Int2(5, 3), south.ChunkSize); // 长 × 高
        Assert.Equal(new Int3(2, 0, 0), south.Offset);

        var north = faces.First(f => f.Direction == Int3.North);
        Assert.Equal(new Int3(2, 0, 1), north.Offset); // 厚度 1 → +Z 侧
    }

    [Fact]
    public void NorthSouthWall_ExposesWestAndEastFaces()
    {
        var wall = new Wall { Height = 4 };
        wall.Coords = new Int3(0, 0, 2);
        var line = (LineShape)wall.Shape;
        line.Start = Float3.Zero;
        line.End = new Float3(0, 0, 4); // 南北走向，长 4

        var faces = wall.Layouts;

        Assert.Equal(2, faces.Count());
        Assert.Contains(faces, f => f.Direction == Int3.West);
        Assert.Contains(faces, f => f.Direction == Int3.East);

        var west = faces.First(f => f.Direction == Int3.West);
        Assert.Equal(new Int2(4, 4), west.ChunkSize); // 高 × 长
        Assert.Equal(new Int3(0, 0, 2), west.Offset);

        var east = faces.First(f => f.Direction == Int3.East);
        Assert.Equal(new Int3(1, 0, 2), east.Offset); // 厚度 1 → +X 侧
    }

    [Fact]
    public void FaceToWorld_MapsOntoTheWallPlane()
    {
        var wall = new Wall { Height = 3 };
        wall.Coords = new Int3(2, 0, 0);
        var line = (LineShape)wall.Shape;
        line.Start = Float3.Zero;
        line.End = new Float3(5, 0, 0);

        var north = wall.Layouts.First(f => f.Direction == Int3.North);

        // 北面：local.X→世界X（沿墙），local.Z→世界Y（高度）
        Assert.Equal(new Int3(2, 0, 1), north.AsAbsolute(new Int2(0, 0)));
        Assert.Equal(new Int3(6, 2, 1), north.AsAbsolute(new Int2(4, 2)));
    }
}
