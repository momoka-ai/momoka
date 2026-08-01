using Momoka.Home.Models;
using Momoka.Home.Models.Entities;
using Momoka.Home.Primitives;

namespace Momoka.Home.Services;

public static class RegionService
{
    public static List<Entity> GetEntitiesInRegion(BlockCompositionEntity space, Region region)
    {
        return space.Entities.Where(e =>
        {
            if (e is BlockEntity be) return region.Contains(be);
            if (e is LivingEntity le) return region.Contains(le.Location.Int2);
            if (e is RobotEntity re) return region.Contains(re.Location.Int2);
            return false;
        }).ToList();
    }
}
