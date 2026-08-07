using Momoka.Home;
using Momoka.Home.Geometry;
using Momoka.Home.Primitives;
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

public class Appliance : Entity
{
    public static readonly EnumProperty<PowerState> POWER = new("power", PowerState.Off);
    public static readonly EnumProperty<ConnectionState> CONNECTION = new("connection", ConnectionState.Offline);
    public static readonly TextureProperty TEXTURE = new("texture");

    public Appliance()
    {
        Volume = new Box3D();
        AddProperty(POWER, CONNECTION, TEXTURE);
    }
}
