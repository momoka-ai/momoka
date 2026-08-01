using Momoka.Home.Models.Entities;
using Momoka.Home.Models.Levels;
using Momoka.Home.Models.Shapes;
using Momoka.Home.Primitives;

namespace Momoka.Home.Services;

public static class WallBuildingService
{
    public static bool BuildWall(Level level, Int2 from, Int2 to, Wall? wall = null)
    {
        wall ??= new Wall();
        var shape = (LineShape)wall.Shape;
        shape.Start = from.ToFloat3();
        shape.End = to.ToFloat3();

        level.WallGraph.AddNode(from);
        level.WallGraph.AddNode(to);
        level.WallGraph.AddEdge(from, to, wall);

        foreach (var cell in shape.Locations())
        {
            level[cell.Int3] = wall;
        }

        level.Entities.Add(wall);
        return true;
    }
}
