
namespace Momoka.Home;

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
