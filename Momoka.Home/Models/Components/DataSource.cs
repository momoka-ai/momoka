using Momoka.Home.Models.States;

namespace Momoka.Home.Models.Components;

public enum DataSourceType
{
    Temperature,
    Humidity,
    Illuminance,
    Pressure,
    Pm25,
    Co2,
    Noise,
    Motion,
    Occupancy,
    DoorContact,
    Smoke,
    WaterLeak,
    Camera
}

public class DataSource : Component
{
    public static readonly EnumProperty<DataSourceType> TYPE = new("type", typeof(DataSource), DataSourceType.Temperature);
    public static readonly FloatProperty VALUE = new("value", typeof(DataSource));

    public DataSource(DataSourceType type)
    {
        AddProperty(TYPE, VALUE);
        SetValue(TYPE, type);
    }
}
