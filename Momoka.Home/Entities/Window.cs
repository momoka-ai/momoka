using Momoka.Home;
using Momoka.Home.Shapes;
using Momoka.Home.States;
namespace Momoka.Home.Entities;

public class Window : VoxelEntity
{
    public static readonly BooleanProperty OPEN = new("open", typeof(Window));
    public static readonly TextureProperty TEXTURE = new("texture", typeof(Window));

    public Window()
    {
        Shape = new BoxShape();
        AddProperty(OPEN, TEXTURE);
    }
}
