using Xunit;
using Momoka.Home;
using Momoka.Home.Levels.Entities;
using Momoka.Home.Levels;
using Momoka.Home.Levels.Commands;
using Momoka.Home.Levels.Volumes;
using Momoka.Home.Levels.Layouts;
using Momoka.Home.Primitives;
using Momoka.Home.Levels.Entities.Components;
using Momoka.Home.Levels.Entities.Properties;
namespace Momoka.Home.Tests.Models.Level;

/// <summary>结构命令：BuildWall / BuildOpening 的创建即放置语义、墙排洞与开口占用语义。</summary>
public class StructureCommandTests
{
    [Fact]
    public void BuildWall_CreatesImmutableWall_WithSurfaceHost()
    {
        var session = Scenes.Session();
        var changes = session.Execute(new BuildWallCommand(new[] { new WallSegment(new Int3(0, 1, 0), new Int3(10, 29, 1)) }));
        Assert.NotNull(changes);

        var wall = session.Layout.Entities.Single(e => e.Key == new Key("wall"));
        Assert.True(wall.IsImmutable());
        Assert.IsType<Box>(wall.Volume);
        Assert.NotNull(wall.GetComponent<PlacementLayoutSource>());
        // 占用格已写入
        Assert.Same(wall, session.Layout.Voxels[new Int3(5, 1, 0)]);
        Assert.Same(wall, session.Layout.Voxels[new Int3(0, 20, 0)]);

        // ChangeSet 含 Added（脏块由客户端本地推导，不在 ChangeSet 上）
        Assert.Equal(EntityChangeKind.Added, Assert.Single(changes!.Changes).Kind);
    }

    [Fact]
    public void BuildWall_MultiSegment_ProducesComposite()
    {
        var session = Scenes.Session();
        session.Execute(new BuildWallCommand(new[]
        {
            new WallSegment(new Int3(0, 1, 0), new Int3(5, 29, 1)),
            new WallSegment(new Int3(5, 1, 0), new Int3(1, 29, 4)), // L 形
        }));

        var wall = session.Layout.Entities.Single(e => e.Key == new Key("wall"));
        var composite = Assert.IsType<Composite>(wall.Volume);
        Assert.Equal(2, composite.Children.Count);
        Assert.Same(wall, session.Layout.Voxels[new Int3(5, 1, 3)]);
        Assert.True(session.Layout.Voxels[new Int3(5, 1, 5)] is null); // L 形缺口
    }

    [Fact]
    public void BuildWall_OutOfWorldExtent_Fails()
    {
        var session = Scenes.Session();
        var huge = new Int3(int.MaxValue / 16, 1, 0);
        Assert.Null(session.Execute(new BuildWallCommand(new[] { new WallSegment(huge, new Int3(1, 1, 1)) })));
        Assert.Empty(session.Layout.Entities);
        Assert.Empty(session.Data.Entities); // 无任何残留
    }

    [Fact]
    public void BuildOpening_PunchesHole_PlacesDoorOnWall()
    {
        var (session, midWall) = Scenes.TwoRoomScene();
        var wallBefore = session.Layout.Find(midWall);
        Assert.NotNull(wallBefore);
        var surfaceBefore = wallBefore!.GetComponent<PlacementLayoutSource>();
        var anchorBefore = session.Layout.Voxels.GetAsRelative(wallBefore!.Transform.Position);
        var punchedBefore = VolumePunch.Punch(wallBefore.Volume, anchorBefore, new Int3(5, 1, 4), new Int3(1, 20, 2));
        Assert.NotNull(surfaceBefore);
        Assert.NotNull(punchedBefore);

        var changes = session.Execute(new BuildOpeningCommand(midWall, new Int3(5, 1, 4), new Int3(1, 20, 2), "door", isOpen: true));
        Assert.NotNull(changes);

        var wall = session.Layout.Find(midWall)!;
        // 墙体积已分段（Composite：左段 + 右段 + 过梁）
        var composite = Assert.IsType<Composite>(wall.Volume);
        Assert.True(composite.GetVoxelSet().Any());
        // 洞口局部格（y<20 且 z∈{3,4}）已从墙体排除
        Assert.DoesNotContain(composite.GetVoxelSet(), c => c.Y < 20 && c.Z is 3 or 4);

        // 门实体占据洞口格（is_open=true，挂宿主墙）
        var door = session.Layout.Entities.Single(e => e.Key == new Key("door"));
        Assert.True(door.GetValue<bool>(Property.IsOpen));
        Assert.True(door.IsImmutable());
        Assert.Same(wall.GetComponent<PlacementLayoutSource>(), session.Layout.FindHostEntity(door));

        // 洞口格不再被墙占据（门在、墙不在）
        Assert.Same(door, session.Layout.Voxels[new Int3(5, 10, 4)]);
        Assert.Same(door, session.Layout.Voxels[new Int3(5, 10, 5)]);
        Assert.NotSame(wall, session.Layout.Voxels[new Int3(5, 10, 5)]);
        Assert.Same(wall, session.Layout.Voxels[new Int3(5, 1, 3)]); // 左段保留
    }

    [Fact]
    public void BuildOpening_InvalidHole_TransactionFailsAtomically()
    {
        var (session, midWall) = Scenes.TwoRoomScene();
        var wall = session.Layout.Find(midWall)!;
        var wallVolumeBefore = wall.Volume;

        // 洞口超出墙体（x=7 不在中墙）→ 整体失败
        Assert.Null(session.Execute(new BuildOpeningCommand(midWall, new Int3(7, 1, 4), new Int3(1, 20, 2), "door")));

        Assert.Same(wallVolumeBefore, wall.Volume); // 墙未改
        Assert.DoesNotContain(session.Data.Entities, e => e.Key == new Key("door")); // 开口未落
        Assert.True(session.Layout.Voxels[new Int3(7, 10, 4)] is null);
    }

    [Fact]
    public void BuildOpening_RemovedDoor_KeepsHole()
    {
        var (session, midWall) = Scenes.TwoRoomScene();
        session.Execute(new BuildOpeningCommand(midWall, new Int3(5, 1, 4), new Int3(1, 20, 2), "door", isOpen: true));
        var doorId = session.Layout.Entities.Single(e => e.Key == new Key("door")).Id;

        // 移除开口实体 → 墙体保留洞口（墙体积不变）
        Assert.True(session.Execute(new RemoveEntityCommand(doorId)) is not null);
        var wall = session.Layout.Find(midWall)!;
        Assert.IsType<Composite>(wall.Volume);
        Assert.Null(session.Layout.Voxels[new Int3(5, 10, 4)]);
    }

    [Fact]
    public void Opening_CollisionAtDoorCells_ReturnsDoorNotWall()
    {
        var (session, midWall) = Scenes.TwoRoomScene();
        session.Execute(new BuildOpeningCommand(midWall, new Int3(5, 1, 4), new Int3(1, 20, 2), "door", isOpen: true));

        // 视线 / 碰撞探测洞口格命中门，而非被墙挡
        var hit = session.Layout.IsCollided(new Position(new Float3(50, 100, 40)));
        Assert.NotNull(hit);
        Assert.Equal("door", hit!.Value.Hit.Key.ToString().Split(':')[1]);
    }
}
