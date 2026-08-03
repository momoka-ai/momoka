using Momoka.Home.Models;
using Momoka.Home.Models.Layouts;
using Momoka.Home.Primitives;
using Xunit;

namespace Momoka.Home.Tests.Models.Layouts;

/// <summary>
/// Planar subdivision face enumeration: two adjacent rooms must resolve to two
/// bounded faces, FaceAt must locate the room containing a point, face-entity
/// binding must survive recomputation, and Merge must combine adjacent rooms.
/// </summary>
public class SubdivisionTests
{
    private static Subdivision<TileEntity> BuildTwoRooms()
    {
        var sub = new Subdivision<TileEntity>();
        sub.AddNode(new Int2(0, 0));
        sub.AddNode(new Int2(2, 0));
        sub.AddNode(new Int2(4, 0));
        sub.AddNode(new Int2(0, 2));
        sub.AddNode(new Int2(2, 2));
        sub.AddNode(new Int2(4, 2));

        sub.AddEdge(new Int2(0, 0), new Int2(2, 0));
        sub.AddEdge(new Int2(2, 0), new Int2(4, 0));
        sub.AddEdge(new Int2(0, 2), new Int2(2, 2));
        sub.AddEdge(new Int2(2, 2), new Int2(4, 2));
        sub.AddEdge(new Int2(0, 0), new Int2(0, 2));
        sub.AddEdge(new Int2(4, 0), new Int2(4, 2));
        sub.AddEdge(new Int2(2, 0), new Int2(2, 2)); // 共享隔墙
        return sub;
    }

    [Fact]
    public void TwoAdjacentRooms_YieldsTwoBoundedFaces()
    {
        var sub = BuildTwoRooms();
        Assert.Equal(2, sub.BoundedFaces.Count);
    }

    [Fact]
    public void FaceAt_LocatesRoomContainingPoint()
    {
        var sub = BuildTwoRooms();
        var left = sub.FaceAt(new Int2(1, 1));
        var right = sub.FaceAt(new Int2(3, 1));

        Assert.NotNull(left);
        Assert.NotNull(right);
        Assert.True(left!.Contains(new Int2(1, 1)));
        Assert.True(right!.Contains(new Int2(3, 1)));
        Assert.False(left.Contains(new Int2(3, 1)));
    }

    [Fact]
    public void AssignEntity_SurvivesRecomputation()
    {
        var sub = BuildTwoRooms();
        var face = sub.FaceAt(new Int2(1, 1))!;
        var tile = new TileEntity();

        sub.AssignEntity(face, tile);

        // FaceAt recomputes faces each call — binding must persist by vertex set.
        Assert.Same(tile, sub.EntityOf(sub.FaceAt(new Int2(1, 1))!));
    }

    [Fact]
    public void Merge_RemovesSharedWall_CombinesRooms()
    {
        var sub = BuildTwoRooms();
        var left = sub.FaceAt(new Int2(1, 1))!;
        var right = sub.FaceAt(new Int2(3, 1))!;

        Assert.True(sub.Merge(left, right));
        Assert.Single(sub.BoundedFaces);
    }

    [Fact]
    public void Merge_NonAdjacentFaces_ReturnsFalse()
    {
        var sub = BuildTwoRooms();
        var face = sub.FaceAt(new Int2(1, 1))!;

        Assert.False(sub.Merge(face, face));
    }
}
