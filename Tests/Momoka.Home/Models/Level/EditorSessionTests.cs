using Xunit;
using Momoka.Home;
using Momoka.Home.Level;
using Momoka.Home.Level.Commands;
using Momoka.Home.Primitives;
namespace Momoka.Home.Tests.Models.Level;

/// <summary>编辑器骨架：会话执行 / 撤销 / 重做与变更管道（LayoutChanged + 脏块）。</summary>
public class EditorSessionTests
{
    [Fact]
    public void Execute_RaisesLayoutChanged_WithEntityAndDirtyChunks()
    {
        var session = Scenes.Session();
        var box = Scenes.Box("box", 2, 1, 2);
        session.Data.Entities.Add(box);

        ChangeSet? raised = null;
        session.LayoutChanged += (_, e) => raised = e.Changes;

        var changes = session.Execute(new PlaceEntityCommand(box.Id, new Float3(50, 0, 50)));
        Assert.NotNull(changes);

        var change = Assert.Single(changes!.Changes);
        Assert.Equal(EntityChangeKind.Added, change.Kind);
        Assert.Same(box, change.Entity);

        Assert.Same(changes, raised);
    }

    [Fact]
    public void Undo_Redo_RoundTrip_RestoresState()
    {
        var session = Scenes.Session();
        var box = Scenes.Box("box", 1, 1, 1);
        session.Data.Entities.Add(box);

        session.Execute(new PlaceEntityCommand(box.Id, new Float3(50, 0, 50)));
        Assert.Single(session.Layout.Entities);

        var undo = session.Undo();
        Assert.NotNull(undo);
        Assert.Single(undo!.Changes);
        Assert.Equal(EntityChangeKind.Removed, undo.Changes[0].Kind);
        Assert.Empty(session.Layout.Entities);
        Assert.True(session.Layout.Voxels[new Int3(5, 0, 5)] is null);

        var redo = session.Redo();
        Assert.NotNull(redo);
        Assert.Equal(EntityChangeKind.Added, redo!.Changes[0].Kind);
        Assert.Single(session.Layout.Entities);
        Assert.True(session.Layout.Voxels[new Int3(5, 0, 5)] is not null);
    }

    [Fact]
    public void Execute_FailedCommand_NotRecorded_NoEvent()
    {
        var session = Scenes.Session();
        var box = Scenes.Box("box", 1, 1, 1);
        session.Data.Entities.Add(box);
        session.Execute(new PlaceEntityCommand(box.Id, new Float3(50, 0, 50)));

        var events = 0;
        session.LayoutChanged += (_, _) => events++;

        // 与已放置实体碰撞 → 失败（不推历史、不发事件）
        var other = Scenes.Box("box", 1, 1, 1);
        session.Data.Entities.Add(other);
        Assert.Null(session.Execute(new PlaceEntityCommand(other.Id, new Float3(50, 0, 50))));

        Assert.Single(session.History.UndoStack); // 只有首次成功放置
        Assert.Equal(0, events);
    }

    [Fact]
    public void Undo_WithNothingToUndo_ReturnsNull()
    {
        var session = Scenes.Session();
        Assert.Null(session.Undo());
        Assert.Null(session.Redo());
    }

    [Fact]
    public void UndoRedo_ModifiedChange_RoundTripsState()
    {
        var session = Scenes.Session();
        var box = Scenes.Box("box", 1, 1, 1);
        session.Data.Entities.Add(box);
        session.Execute(new PlaceEntityCommand(box.Id, new Float3(50, 0, 50)));

        session.Execute(new RotateEntityCommand(box.Id, new Float3(90, 0, 0)));
        Assert.Equal(90, box.Transform.Rotation.Yaw);

        var undo = session.Undo();
        var modified = Assert.Single(undo!.Changes);
        Assert.Equal(EntityChangeKind.Modified, modified.Kind);
        Assert.Same(box, modified.Entity);
        Assert.Equal(0, box.Transform.Rotation.Yaw);

        var redo = session.Redo();
        Assert.Equal(EntityChangeKind.Modified, Assert.Single(redo!.Changes).Kind);
        Assert.Equal(90, box.Transform.Rotation.Yaw);
    }
}
