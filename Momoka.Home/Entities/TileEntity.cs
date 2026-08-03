using Momoka.Home;
using Momoka.Home.States;
namespace Momoka.Home.Entities;

public class TileEntity : Entity
{
    public static readonly TextureProperty TEXTURE = new("texture", typeof(TileEntity));

    public TileEntity()
    {
        AddProperty(TEXTURE);
    }
}
