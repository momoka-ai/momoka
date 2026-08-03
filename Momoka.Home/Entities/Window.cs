using Momoka.Home.Models.Shapes;
using Momoka.Home.Models.States;

namespace Momoka.Home.Models.Entities;

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
