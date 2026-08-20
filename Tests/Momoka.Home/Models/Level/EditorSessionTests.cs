using Xunit;
using Momoka.Home.Level;
using Momoka.Home.Level.Commands;
using Momoka.Home.Primitives;
namespace Momoka.Home.Tests.Models.Level;

/// <summary>编辑器骨架：会话执行与变更管道（ChangeSet 组装、失败即无变更）。</summary>
public class EditorSessionTests
{
    [Fact]
    public void Execute_ReturnsChangeSet_WithAddedEntity()
    {
        var session = Scenes.Session();
        var box = Scenes.Box("box", 2, 1, 2);
        session.Data.Entities.Add(box);

        var changes = session.Execute(new PlaceEntityCommand(box.Id, new Float3(50, 0, 50)));
        Assert.NotNull(changes);

        var change = Assert.Single(changes!.Changes);
        Assert.Equal(EntityChangeKind.Added, change.Kind);
        Assert.Same(box, change.Entity);
    }

    [Fact]
    public void Execute_FailedCommand_ReturnsNull_NoChange()
    {
        var session = Scenes.Session();
        var box = Scenes.Box("box", 1, 1, 1);
        session.Data.Entities.Add(box);
        session.Execute(new PlaceEntityCommand(box.Id, new Float3(50, 0, 50)));

        // 与已放置实体碰撞 → 失败（无任何变更）
        var other = Scenes.Box("box", 1, 1, 1);
        session.Data.Entities.Add(other);
        Assert.Null(session.Execute(new PlaceEntityCommand(other.Id, new Float3(50, 0, 50))));
        Assert.Single(session.Layout.Entities);
    }

    [Fact]
    public void Execute_ModifiedChange_ReturnsModified()
    {
        var session = Scenes.Session();
        var box = Scenes.Box("box", 1, 1, 1);
        session.Data.Entities.Add(box);
        session.Execute(new PlaceEntityCommand(box.Id, new Float3(50, 0, 50)));

        var modified = session.Execute(new RotateEntityCommand(box.Id, new Float3(90, 0, 0)));
        Assert.NotNull(modified);
        Assert.Equal(EntityChangeKind.Modified, Assert.Single(modified!.Changes).Kind);
        Assert.Same(box, modified.Changes[0].Entity);
        Assert.Equal(90, box.Transform.Rotation.Yaw);
    }
}
