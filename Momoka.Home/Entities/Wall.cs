using Momoka.Home;
using Momoka.Home.Layouts;
using Momoka.Home.Primitives;
using Momoka.Home.Shapes;
using Momoka.Home.States;
namespace Momoka.Home.Entities;

/// <summary>
/// A wall segment: a straight line (see <see cref="LineShape"/>) with a
/// thickness. Geometry lives on the floor-plan graph; the two faces are exposed
/// as placement surfaces by <see cref="FloorPlanLayout.Surfaces"/>, computed on
/// demand from the edge span and this instance's property table. A thin data
/// shell — config-driven partitions will replace it once template materialization
/// lands.
/// </summary>
public class Wall : Entity<Int3>
{
    public static readonly TextureProperty TEXTURE = new("texture", new Key("wall"));
    public static readonly BooleanProperty USE_VOXEL_LAYOUT = new(FloorPlanLayout.UseVoxelLayoutProperty, new Key("wall"), true);
    public static readonly IntProperty HEIGHT = new(FloorPlanLayout.HeightProperty, new Key("wall"), 3);
    public static readonly IntProperty THICKNESS = new(FloorPlanLayout.ThicknessProperty, new Key("wall"), 1);

    public Wall()
    {
        Shape = new LineShape();
        // 每实例克隆：Property.Value 是实例级状态，直接挂静态定义会让所有 Wall 共享值
        AddProperty(TEXTURE.Clone(), USE_VOXEL_LAYOUT.Clone(), HEIGHT.Clone(), THICKNESS.Clone());
    }
}
