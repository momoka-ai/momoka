using Momoka.Home;
using Momoka.Home.Primitives;
namespace Momoka.Home.Entities;

public abstract class LivingEntity : Entity
{
    public Float3 Location { get; set; }
    public Float3 Velocity { get; set; }
}
