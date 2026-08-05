using Momoka.Home;
using Momoka.Home.Geometry;
using Momoka.Home.Primitives;
using Momoka.Home.States;
namespace Momoka.Home.Entities;

public class Door : Entity<Int3>
{
    public static readonly BooleanProperty OPEN = new("open");
    public static readonly BooleanProperty LOCKED = new("locked");
    public static readonly TextureProperty TEXTURE = new("texture");

    public Door()
    {
        Volume = new Box3D();
        AddProperty(OPEN, LOCKED, TEXTURE);
    }
}
