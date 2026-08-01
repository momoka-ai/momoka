using Momoka.Home.Models.Entities;
using Momoka.Home.Primitives;

namespace Momoka.Home.Services;

public static class PlacementService
{
    public static bool CanPlace(BlockCompositionEntity space, BlockEntity entity, Int3 pos)
    {
        // Occupied by another BlockEntity?
        if (space.HasEntity(pos))
            return false;

        // Embedded entities need a host surface
        if (entity.Parent is BlockEntity host)
        {
            foreach (var cell in entity.Shape.Locations())
            {
                if (space[cell.Int3] == host)
                    return true;
            }
            return false;
        }

        // Non-embedded: check no collision in entity's shape area
        foreach (var cell in entity.Shape.Locations())
        {
            if (space.HasEntity(cell.Int3))
                return false;
        }

        return true;
    }

    public static bool Place(BlockCompositionEntity space, BlockEntity entity, Int3 pos)
    {
        if (!CanPlace(space, entity, pos))
            return false;

        space[pos] = entity;
        space.Entities.Add(entity);
        return true;
    }
}
