using Momoka.Home.Models.Shapes;
using Momoka.Home.Models.States;

namespace Momoka.Home.Models.Entities;

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

public class Appliance : BlockEntity
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
