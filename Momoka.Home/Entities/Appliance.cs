using Momoka.Home;
using Momoka.Home.Shapes;
using Momoka.Home.States;
namespace Momoka.Home.Entities;

public enum PowerState
{
    Off,
    On,
    Standby
}

public enum ConnectionState
{
    Offline,
    Online,
    Paired
}

public class Appliance : VoxelEntity
{
    public static readonly EnumProperty<PowerState> POWER = new("power", typeof(Appliance), PowerState.Off);
    public static readonly EnumProperty<ConnectionState> CONNECTION = new("connection", typeof(Appliance), ConnectionState.Offline);
    public static readonly TextureProperty TEXTURE = new("texture", typeof(Appliance));

    public Appliance()
    {
        Shape = new BoxShape();
        AddProperty(POWER, CONNECTION, TEXTURE);
    }
}
