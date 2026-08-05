using Momoka.Home;
using Momoka.Home.Geometry;
using Momoka.Home.Primitives;
using Momoka.Home.States;
namespace Momoka.Home.Entities;

public class Window : Entity<Int3>
{
    public static readonly BooleanProperty OPEN = new("open");
    public static readonly TextureProperty TEXTURE = new("texture");

    public Window()
    {
        Volume = new Box3D();
        AddProperty(OPEN, TEXTURE);
    }
}
