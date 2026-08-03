using Momoka.Home;
using Momoka.Home.Primitives;
using Momoka.Home.States;
namespace Momoka.Home.Entities;

public class TileEntity : Entity
{
    public static readonly TextureProperty TEXTURE = new("texture", new Key("tileentity"));

    public TileEntity()
    {
        AddProperty(TEXTURE);
    }
}
