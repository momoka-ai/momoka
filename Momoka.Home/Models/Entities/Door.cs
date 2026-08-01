using Momoka.Home.Models.Shapes;
using Momoka.Home.Models.States;

namespace Momoka.Home.Models.Entities;

public class Door : BlockEntity
{
    public static readonly BooleanProperty OPEN = new("open", typeof(Door));
    public static readonly BooleanProperty LOCKED = new("locked", typeof(Door));
    public static readonly TextureProperty TEXTURE = new("texture", typeof(Door));

    public Door()
    {
        Shape = new BoxShape();
        AddProperty(OPEN, LOCKED, TEXTURE);
    }
}
