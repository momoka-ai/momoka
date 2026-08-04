using Momoka.Home;
using Momoka.Home.Primitives;
using Momoka.Home.Shapes;
using Momoka.Home.States;
namespace Momoka.Home.Entities;

public class Door : Entity<Int3>
{
    public static readonly BooleanProperty OPEN = new("open", new Key("door"));
    public static readonly BooleanProperty LOCKED = new("locked", new Key("door"));
    public static readonly TextureProperty TEXTURE = new("texture", new Key("door"));

    public Door()
    {
        Shape = new BoxShape();
        AddProperty(OPEN, LOCKED, TEXTURE);
    }
}
