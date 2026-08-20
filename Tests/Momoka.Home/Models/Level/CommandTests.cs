using Xunit;
using Momoka.Home.Level;
using Momoka.Home.Level.Commands;
using Momoka.Home.Primitives;
using Momoka.Home.Entities.Components;
using Momoka.Home.Entities.Properties;
namespace Momoka.Home.Tests.Models.Level;

/// <summary>基础命令：Place / Remove / Move / Rotate / SetProperty 的正向语义与 ChangeSet 组装。</summary>
public class CommandTests
{
    [Fact]
    public void Place_Remove_RoundTrip()
    {
        var session = Scenes.Session();
        var box = Scenes.Box("box", 1, 1, 1);
        session.Data.Entities.Add(box);

        session.Execute(new PlaceEntityCommand(box.Id, new Float3(50, 0, 50)));
        Assert.Single(session.Layout.Entities);

        Assert.True(session.Execute(new RemoveEntityCommand(box.Id)) is not null);
        Assert.Empty(session.Layout.Entities);
    }

    [Fact]
    public void Remove_CascadesChildrenBackToPool()
    {
        var session = Scenes.Session();
        var floor = Scenes.Floor(5, 1, 5);
        var floorId = Scenes.Place(session, floor, new Float3(0, 0, 0));
        var floorSurface = floor.GetComponent<PlacementLayoutSource>()!;

        var mug = Scenes.Box("mug", 1, 1, 1);
        session.Data.Entities.Add(mug);
        Assert.True(session.Execute(new PlaceEntityCommand(mug.Id, new Float3(0, 10, 0), floorId)) is not null);
        Assert.Same(mug, Assert.Single(floorSurface.Entities));

        // 删除地板 → 连带回落杯子（两者回到池，实体保留在注册表）
        var changes = session.Execute(new RemoveEntityCommand(floorId));
        Assert.NotNull(changes);
        Assert.Empty(session.Layout.Entities);
        Assert.Equal(2, changes!.Changes.Count); // floor + mug 都标记 Removed
        Assert.Contains(session.Data.Entities, e => e.Id == mug.Id);
    }

    [Fact]
    public void Move_WithHostedChild_MovesChildAlong()
    {
        var session = Scenes.Session();
        var floor = Scenes.Floor(5, 1, 5);
        var floorId = Scenes.Place(session, floor, new Float3(0, 0, 0));
        var floorSurface = floor.GetComponent<PlacementLayoutSource>()!;

        var mug = Scenes.Box("mug", 1, 1, 1);
        session.Data.Entities.Add(mug);
        session.Execute(new PlaceEntityCommand(mug.Id, new Float3(0, 10, 0), floorId));

        // 移动地板 +30cm X → 杯子随宿主同位移
        session.Execute(new MoveEntityCommand(floorId, new Float3(30, 0, 0)));
        Assert.Equal(new Float3(30, 0, 0), floor.Transform.Position);
        Assert.Equal(new Float3(30, 10, 0), mug.Transform.Position);
        Assert.Same(mug, Assert.Single(floorSurface.Entities));
    }

    [Fact]
    public void Move_ToRoot_ClearsHostRegistration()
    {
        var session = Scenes.Session();
        var floor = Scenes.Floor(5, 1, 5);
        var floorId = Scenes.Place(session, floor, new Float3(0, 0, 0));
        var floorSurface = floor.GetComponent<PlacementLayoutSource>()!;

        var mug = Scenes.Box("mug", 1, 1, 1);
        session.Data.Entities.Add(mug);
        session.Execute(new PlaceEntityCommand(mug.Id, new Float3(0, 10, 0), floorId));
        Assert.Same(floorSurface, session.Layout.FindHostEntity(mug));

        // 移到根（无宿主）
        session.Execute(new MoveEntityCommand(mug.Id, new Float3(50, 0, 50)));
        Assert.Null(session.Layout.FindHostEntity(mug));
        Assert.Empty(floorSurface.Entities);
    }

    [Fact]
    public void Rotate_AppliesDelta()
    {
        var session = Scenes.Session();
        var box = Scenes.Box("box", 1, 1, 1);
        var id = Scenes.Place(session, box, new Float3(50, 0, 50));

        Assert.True(session.Execute(new RotateEntityCommand(id, new Float3(90, 0, 0))) is not null);
        Assert.Equal(90, box.Transform.Rotation.Yaw);
    }

    [Fact]
    public void SetProperty_SetsValue()
    {
        var session = Scenes.Session();
        var door = Scenes.Box("door", 1, 1, 1);
        door.AddProperty(new BooleanProperty(Property.IsOpen, false), new BooleanProperty(Property.IsImmutable, true));
        var id = Scenes.Place(session, door, new Float3(50, 0, 50));

        Assert.True(session.Execute(new SetPropertyCommand(id, Property.IsOpen, true)) is not null);
        Assert.True(door.GetValue<bool>(Property.IsOpen));
    }

    [Fact]
    public void SetProperty_MissingProperty_Fails()
    {
        var session = Scenes.Session();
        var box = Scenes.Box("box", 1, 1, 1);
        var id = Scenes.Place(session, box, new Float3(50, 0, 50));

        Assert.Null(session.Execute(new SetPropertyCommand(id, "no_such_property", true)));
        Assert.Null(box.Properties.FirstOrDefault(p => p.Name == "no_such_property"));
    }

    [Fact]
    public void SetProperty_CreateIfMissing_AddsAndClears()
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
    }
}
