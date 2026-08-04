using Momoka.Home;
using Momoka.Home.Primitives;
using Momoka.Home.Shapes;
using Momoka.Home.States;
namespace Momoka.Home.Entities;

public class Window : Entity<Int3>
{
    public static readonly BooleanProperty OPEN = new("open", new Key("window"));
    public static readonly TextureProperty TEXTURE = new("texture", new Key("window"));

    public Window()
    {
        Shape = new BoxShape();
        AddProperty(OPEN, TEXTURE);
    }
}
