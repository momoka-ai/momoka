using Momoka.Home.Runtime;
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

/// <summary>编辑测试共用的场景搭建（实体 / 会话 / 双室场景）。</summary>
internal static class Scenes
{
    public static Entity Box(string path, int sx, int sy, int sz) => new()
    {
        Key = new Key(path),
        Volume = new Box3D { SizeX = sx, SizeY = sy, SizeZ = sz },
    };

    public static EditorSession Session() => new(new LevelData());

    /// <summary>地板：is_immutable + Up 放置面（站立格 y=1）。</summary>
    public static Entity Floor(int sx = 10, int sy = 1, int sz = 10)
    {
        var floor = Box("floor", sx, sy, sz);
        floor.AddProperty(new BooleanProperty(Property.IsImmutable, true));
        var surface = new GridLayout<bool>(new Int2(sx, sz));
        surface.Fill(true, Int2.Zero, new Int2(sx, sz));
        floor.AddComponent(new PlacementLayoutSource
        {
            Layout = surface,
            Transform = new Transform(new Float3(0, 10, 0), Rotation.Up),
        });
        return floor;
    }

    public static Entity Ceiling(int sx = 10, int sz = 10)
    {
        var ceiling = Box("ceiling", sx, 1, sz);
        ceiling.AddProperty(new BooleanProperty(Property.IsImmutable, true));
        return ceiling;
    }

    /// <summary>把实体登记进池并按位置放置，返回其 Id。</summary>
    public static Guid Place(EditorSession session, Entity entity, Float3 position)
    {
        session.Data.Entities.Add(entity);
        var result = session.Execute(new PlaceEntityCommand(entity.Id, position));
        if (result is null)
            throw new InvalidOperationException($"Place failed for '{entity.Key}'.");
        return entity.Id;
    }

    /// <summary>
    /// 10×30×10 双室场景（Region 用）：地板 + 天花板 + 四围墙 + 中墙 x=5（z=1..8），
    /// 左室 x=1..4、右室 x=6..8。返回会话与中墙 Id。
    /// </summary>
    public static (EditorSession Session, Guid MidWall) TwoRoomScene()
    {
        var session = Session();
        Place(session, Floor(), new Float3(0, 0, 0));
        Place(session, Ceiling(), new Float3(0, 300, 0));

        var midWall = Guid.Empty;
        BuildWall(session, new Int3(0, 1, 0), new Int3(10, 29, 1)); // 北 z=0
        BuildWall(session, new Int3(0, 1, 9), new Int3(10, 29, 1)); // 南 z=9
        BuildWall(session, new Int3(0, 1, 1), new Int3(1, 29, 8)); // 西 x=0
        BuildWall(session, new Int3(9, 1, 1), new Int3(1, 29, 8)); // 东 x=9
        midWall = BuildWall(session, new Int3(5, 1, 1), new Int3(1, 29, 8)); // 中 x=5
        return (session, midWall);
    }

    /// <summary>在会话中砌一段墙，返回墙实体 Id。</summary>
    public static Guid BuildWall(EditorSession session, Int3 origin, Int3 size)
    {
        var command = new BuildWallCommand(new[] { new WallSegment(origin, size) });
        if (session.Execute(command) is null)
            throw new InvalidOperationException("BuildWall failed.");
        var wall = session.Layout.Entities.Single(e => e.Key == new Key("wall") &&
            session.Layout.Voxels.GetAsRelative(e.Transform.Position) == origin);
        return wall.Id;
    }
}
