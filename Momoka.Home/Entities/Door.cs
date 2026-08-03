using Momoka.Home;
using Momoka.Home.Shapes;
using Momoka.Home.States;
namespace Momoka.Home.Entities;

public class Door : VoxelEntity
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
