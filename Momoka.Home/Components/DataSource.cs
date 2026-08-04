namespace Momoka.Home.Components;

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

/// <summary>A sensor data source: a typed carrier of the measured quantity.</summary>
public class DataSource : Component
{
    public DataSourceType Type { get; set; }
    public float Value { get; set; }

    public DataSource(DataSourceType type)
    {
        Type = type;
    }
}
