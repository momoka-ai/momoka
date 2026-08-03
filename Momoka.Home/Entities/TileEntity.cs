using Momoka.Home.Models.Entities;
using Momoka.Home.Models.States;

namespace Momoka.Home.Models;

public class TileEntity : Entity
{
    public static readonly TextureProperty TEXTURE = new("texture", typeof(TileEntity));

    public TileEntity()
    {
        AddProperty(TEXTURE);
    }
}
