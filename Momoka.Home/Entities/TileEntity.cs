
namespace Momoka.Home;

public class TileEntity : Entity
{
    public static readonly TextureProperty TEXTURE = new("texture", typeof(TileEntity));

    public TileEntity()
    {
        AddProperty(TEXTURE);
    }
}
