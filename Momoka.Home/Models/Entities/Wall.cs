using Momoka.Home.Models.Shapes;
using Momoka.Home.Models.States;

namespace Momoka.Home.Models.Entities;

public class Wall : VoxelEntity
{
    public static readonly TextureProperty TEXTURE = new("texture", typeof(Wall));

    public Wall()
    {
        Shape = new LineShape();
        AddProperty(TEXTURE);
    }
}
