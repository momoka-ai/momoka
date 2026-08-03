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
        // Shape is local: anchor the wall at `from`, the segment is relative.
        wall.Coords = from.ToInt3();
        shape.Start = Float3.Zero;
        shape.End = (to - from).ToFloat3();

        level.Boundary.AddNode(from);
        level.Boundary.AddNode(to);
        level.Boundary.AddEdge(from, to, wall);

        foreach (var cell in shape.GetVoxels())
        {
            level[wall.Coords + cell] = wall;
        }

        level.Entities.Add(wall);
        return true;
    }
}
