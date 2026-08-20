using Xunit;
using Momoka.Home;
using Momoka.Home.Entities;
using Momoka.Home.Editing;
using Momoka.Home.Editing.Commands;
using Momoka.Home.Primitives;
namespace Momoka.Home.Tests.Models.Editing;

/// <summary>基础命令：Place / Remove / Move / Rotate / SetProperty / SetTexture 的
/// undo/redo 往返、Move 合并（CoalesceKey）与级联链恢复。</summary>
public class CommandTests
{
    [Fact]
    public void Place_Remove_Undo_Redo_RoundTrip()
    {
        var session = Scenes.Session();
        var box = Scenes.Box("box", 1, 1, 1);
        session.Residence.Entities.Add(box);

        session.Execute(new PlaceEntityCommand(box.Id, new Float3(50, 0, 50)));
        Assert.True(session.Execute(new RemoveEntityCommand(box.Id)) is not null);
        Assert.Empty(session.Layout.Entities);

        session.Undo(); // 恢复放置
        Assert.Single(session.Layout.Entities);
        Assert.Equal(new Float3(50, 0, 50), box.Transform.Position);

        session.Redo(); // 再次删除
        Assert.Empty(session.Layout.Entities);
    }

    [Fact]
    public void Remove_Undo_RestoresCascadeChain()
    {
        var session = Scenes.Session();
        var floor = Scenes.Floor(5, 1, 5);
        var floorId = Scenes.Place(session, floor, new Float3(0, 0, 0));
        var floorSurface = floor.GetComponent<PlacementLayoutSource>()!;

        var mug = Scenes.Box("mug", 1, 1, 1);
        mug.AddProperty(new EnumProperty<RotationAlignment>(Property.RotationAlignment, RotationAlignment.Upside));
        session.Residence.Entities.Add(mug);
        Assert.True(session.Execute(new PlaceEntityCommand(mug.Id, new Float3(0, 10, 0), floorId)) is not null);
        Assert.Same(mug, Assert.Single(floorSurface.Entities));

        // 删除地板 → 连带回落杯子
        Assert.True(session.Execute(new RemoveEntityCommand(floorId)) is not null);
        Assert.Empty(session.Layout.Entities);

        // 撤销 → 地板 + 杯子按原宿主恢复
        Assert.True(session.Undo() is not null);
        Assert.Equal(2, session.Layout.Entities.Count);
        Assert.Same(mug, Assert.Single(floorSurface.Entities));
        Assert.Equal(new Float3(0, 10, 0), mug.Transform.Position);
        Assert.Same(floorSurface, session.Layout.FindHostEntity(mug));
    }

    [Fact]
    public void Move_CoalescesIntoOneHistoryItem_UndoReturnsToStart()
    {
        var session = Scenes.Session();
        var box = Scenes.Box("box", 1, 1, 1);
        var id = Scenes.Place(session, box, new Float3(50, 0, 50));

        session.Execute(new MoveEntityCommand(id, new Float3(60, 0, 50)));
        session.Execute(new MoveEntityCommand(id, new Float3(70, 0, 50)));
        session.Execute(new MoveEntityCommand(id, new Float3(80, 0, 50)));

        // 连续同 CoalesceKey 合并为一个历史项（Place + 合并的 Move）
        var top = Assert.IsType<CoalescedCommand>(session.History.UndoStack[^1]);
        Assert.Equal(3, top.Children.Count);

        // 一次撤销回到拖拽前
        session.Undo();
        Assert.Equal(new Float3(50, 0, 50), box.Transform.Position);
        Assert.IsType<PlaceEntityCommand>(session.History.UndoStack[^1]); // 拖拽项已撤销，栈顶回到放置

        // 重做回到终点
        session.Redo();
        Assert.Equal(new Float3(80, 0, 50), box.Transform.Position);
    }

    [Fact]
    public void Move_WithHostedChild_MovesChildAlong()
    {
        var session = Scenes.Session();
        var floor = Scenes.Floor(5, 1, 5);
        var floorId = Scenes.Place(session, floor, new Float3(0, 0, 0));
        var floorSurface = floor.GetComponent<PlacementLayoutSource>()!;

        var mug = Scenes.Box("mug", 1, 1, 1);
        session.Residence.Entities.Add(mug);
        session.Execute(new PlaceEntityCommand(mug.Id, new Float3(0, 10, 0), floorId));

        // 移动地板 +30cm X → 杯子随宿主同位移
        session.Execute(new MoveEntityCommand(floorId, new Float3(30, 0, 0)));
        Assert.Equal(new Float3(30, 0, 0), floor.Transform.Position);
        Assert.Equal(new Float3(30, 10, 0), mug.Transform.Position);
        Assert.Same(mug, Assert.Single(floorSurface.Entities));

        // 撤销 → 双双回原位
        session.Undo();
        Assert.Equal(new Float3(0, 0, 0), floor.Transform.Position);
        Assert.Equal(new Float3(0, 10, 0), mug.Transform.Position);
    }

    [Fact]
    public void Move_Undo_Redo_RestoresHostRegistration()
    {
        var session = Scenes.Session();
        var floor = Scenes.Floor(5, 1, 5);
        var floorId = Scenes.Place(session, floor, new Float3(0, 0, 0));
        var floorSurface = floor.GetComponent<PlacementLayoutSource>()!;

        var mug = Scenes.Box("mug", 1, 1, 1);
        session.Residence.Entities.Add(mug);
        session.Execute(new PlaceEntityCommand(mug.Id, new Float3(0, 10, 0), floorId));
        Assert.Same(floorSurface, session.Layout.FindHostEntity(mug));

        // 移到根（无宿主）
        session.Execute(new MoveEntityCommand(mug.Id, new Float3(50, 0, 50)));
        Assert.Null(session.Layout.FindHostEntity(mug));
        Assert.Empty(floorSurface.Entities);

        session.Undo();
        Assert.Same(floorSurface, session.Layout.FindHostEntity(mug));
        Assert.Same(mug, Assert.Single(floorSurface.Entities));

        session.Redo();
        Assert.Null(session.Layout.FindHostEntity(mug));
    }

    [Fact]
    public void Rotate_Undo_Redo_RoundTrip()
    {
        var session = Scenes.Session();
        var box = Scenes.Box("box", 1, 1, 1);
        var id = Scenes.Place(session, box, new Float3(50, 0, 50));

        session.Execute(new RotateEntityCommand(id, new Float3(90, 0, 0))); // yaw +90°
        Assert.Equal(90, box.Transform.Rotation.Yaw);
        session.Undo();
        Assert.Equal(0, box.Transform.Rotation.Yaw);
        session.Redo();
        Assert.Equal(90, box.Transform.Rotation.Yaw);
    }

    [Fact]
    public void Rotate_CoalescesLikeMove()
    {
        var session = Scenes.Session();
        var box = Scenes.Box("box", 1, 1, 1);
        var id = Scenes.Place(session, box, new Float3(50, 0, 50));

        session.Execute(new RotateEntityCommand(id, new Float3(0, 30, 0)));
        session.Execute(new RotateEntityCommand(id, new Float3(0, 30, 0)));
        var top = Assert.IsType<CoalescedCommand>(session.History.UndoStack[^1]);
        Assert.Equal(2, top.Children.Count);

        session.Undo();
        Assert.Equal(0, box.Transform.Rotation.Pitch);
    }

    [Fact]
    public void SetProperty_Undo_Redo_RoundTrip()
    {
        var session = Scenes.Session();
        var door = Scenes.Box("door", 1, 1, 1);
        door.AddProperty(new BooleanProperty(Property.IsOpen, false), new BooleanProperty(Property.IsImmutable, true));
        var id = Scenes.Place(session, door, new Float3(50, 0, 50));

        Assert.True(session.Execute(new SetPropertyCommand(id, Property.IsOpen, true)) is not null);
        Assert.True(door.GetValue<bool>(Property.IsOpen));

        session.Undo();
        Assert.False(door.GetValue<bool>(Property.IsOpen));

        session.Redo();
        Assert.True(door.GetValue<bool>(Property.IsOpen));
    }

    [Fact]
    public void SetProperty_MissingProperty_Fails()
    {
        var session = Scenes.Session();
        var box = Scenes.Box("box", 1, 1, 1);
        var id = Scenes.Place(session, box, new Float3(50, 0, 50));

        Assert.Null(session.Execute(new SetPropertyCommand(id, "no_such_property", true)));
        Assert.Single(session.History.UndoStack); // 只有首次放置被记录
    }

    [Fact]
    public void SetProperty_CreateIfMissing_AddsClears_RoundTrip()
    {
        var session = Scenes.Session();
        var wall = Scenes.Box("wall", 1, 1, 1);
        var id = Scenes.Place(session, wall, new Float3(50, 0, 50));

        // 按需补建（重涂：set_texture 路由到 SetPropertyCommand createIfMissing）
        Assert.True(session.Execute(new SetPropertyCommand(id, Property.Texture, "momoka:wood", createIfMissing: true)) is not null);
        Assert.Equal("momoka:wood", wall.GetValue<string>(Property.Texture));

        // 清除（回到无值）
        Assert.True(session.Execute(new SetPropertyCommand(id, Property.Texture, null, createIfMissing: true)) is not null);
        Assert.Equal("", wall.GetValue<string>(Property.Texture));

        // 撤销两次 → 回到有 texture → 回到无属性
        session.Undo();
        Assert.Equal("momoka:wood", wall.GetValue<string>(Property.Texture));
        session.Undo();
        Assert.Null(wall.Properties.FirstOrDefault(p => p.Name == Property.Texture));
    }
}
