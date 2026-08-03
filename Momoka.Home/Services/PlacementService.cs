using Momoka.Home.Models.Entities;
using Momoka.Home.Primitives;

namespace Momoka.Home.Services;

public static class PlacementService
{
    public static bool CanPlace(VoxelGridEntity space, VoxelEntity entity, Int3 pos)
    {
        // Occupied by another VoxelEntity?
        if (space.HasEntity(pos))
            return false;

        // Check no collision in entity's shape area
        foreach (var cell in entity.Shape.GetVoxels())
        {
            if (space.HasEntity(cell))
                return false;
        }

        return true;
    }

    public static bool Place(VoxelGridEntity space, VoxelEntity entity, Int3 pos)
    {
        if (!CanPlace(space, entity, pos))
            return false;

        space[pos] = entity;
        space.Entities.Add(entity);
        return true;
    }
}
