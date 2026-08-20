using Xunit;
using Momoka.Home;
using Momoka.Home.Entities;
using Momoka.Home.Editing;
using Momoka.Home.Editing.Commands;
using Momoka.Home.Geometry;
using Momoka.Home.Layouts;
using Momoka.Home.Primitives;
namespace Momoka.Home.Tests.Models.Editing;

/// <summary>结构命令：BuildWall / BuildOpening 的事务性、墙排洞语义与开口占用语义
/// （碰撞 / Region 连通随 is_open 两态）。</summary>
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
        Assert.IsType<Box3D>(wall.Volume);
        Assert.NotNull(wall.GetComponent<PlacementLayoutSource>());
        // 占用格已写入
        Assert.Same(wall, session.Layout.Voxels[new Int3(5, 1, 0)]);
        Assert.Same(wall, session.Layout.Voxels[new Int3(0, 20, 0)]);

        // ChangeSet 含 Added（脏块由客户端本地推导，不在 ChangeSet 上）
        Assert.Equal(EntityChangeKind.Added, Assert.Single(changes!.Changes).Kind);
    }

    [Fact]
    public void BuildWall_Undo_RemovesWall()
    {
        var session = Scenes.Session();
        session.Execute(new BuildWallCommand(new[] { new WallSegment(new Int3(0, 1, 0), new Int3(10, 29, 1)) }));
        var wallId = session.Layout.Entities.Single(e => e.Key == new Key("wall")).Id;

        Assert.True(session.Undo() is not null);
        Assert.Empty(session.Layout.Entities);
        Assert.True(session.Layout.Voxels[new Int3(5, 1, 0)] is null);

        Assert.True(session.Redo() is not null);
        Assert.Contains(session.Layout.Entities, e => e.Id == wallId);
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
        var composite = Assert.IsType<Composite3D>(wall.Volume);
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
        Assert.False(session.History.CanUndo);
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
        // 墙体积已分段（Composite3D：左段 + 右段 + 过梁）
        var composite = Assert.IsType<Composite3D>(wall.Volume);
        Assert.True(composite.Cells3D().Any());
        // 洞口局部格（y<20 且 z∈{3,4}）已从墙体排除
        Assert.DoesNotContain(composite.Cells3D(), c => c.Y < 20 && c.Z is 3 or 4);

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
    public void BuildOpening_Undo_RestoresWallAndRemovesDoor()
    {
        var (session, midWall) = Scenes.TwoRoomScene();
        session.Execute(new BuildOpeningCommand(midWall, new Int3(5, 1, 4), new Int3(1, 20, 2), "door", isOpen: true));
        var doorId = session.Layout.Entities.Single(e => e.Key == new Key("door")).Id;

        Assert.True(session.Undo() is not null);
        var wall = session.Layout.Find(midWall)!;
        Assert.IsType<Box3D>(wall.Volume); // 原体积还原
        Assert.Null(session.Layout.Find(doorId));
        Assert.DoesNotContain(session.Residence.Entities, e => e.Key == new Key("door"));
        Assert.Same(wall, session.Layout.Voxels[new Int3(5, 10, 4)]); // 洞口格回到墙
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
        Assert.DoesNotContain(session.Residence.Entities, e => e.Key == new Key("door")); // 开口未落
        Assert.DoesNotContain(session.History.UndoStack, c => c.Name == "BuildOpening"); // 未推历史
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
        Assert.IsType<Composite3D>(wall.Volume);
        Assert.Null(session.Layout.Voxels[new Int3(5, 10, 4)]);

        // 撤销 → 门恢复（挂回原宿主）
        Assert.True(session.Undo() is not null);
        var door = session.Layout.Find(doorId)!;
        Assert.Same(door, session.Layout.Voxels[new Int3(5, 10, 4)]);
        Assert.Same(wall.GetComponent<PlacementLayoutSource>(), session.Layout.FindHostEntity(door));
    }

    [Fact]
    public void Opening_RegionSplitsWhenClosed_MergesWhenOpen()
    {
        var (session, midWall) = Scenes.TwoRoomScene();
        session.RebuildRegions();

        // 关门：两室分离
        session.Execute(new BuildOpeningCommand(midWall, new Int3(5, 1, 4), new Int3(1, 20, 2), "door", isOpen: false));
        var closed = session.Regions.Regions!;
        Assert.NotEqual(closed.At(2, 5, 2)!.Id, closed.At(7, 5, 2)!.Id);

        // 开门（SetProperty is_open=true）：两室连通
        var door = session.Layout.Entities.Single(e => e.Key == new Key("door"));
        session.Execute(new SetPropertyCommand(door.Id, Property.IsOpen, true));
        var opened = session.Regions.Regions!;
        Assert.Equal(opened.At(2, 5, 2)!.Id, opened.At(7, 5, 2)!.Id);
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
