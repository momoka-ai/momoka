
namespace Momoka.Home;

public enum EventType
{
    ButtonPress,
    MotionDetected,
    ContactChanged,
    ThresholdReached
}

public class EventSource : Component
{
    public static readonly EnumProperty<EventType> TYPE = new("event_type", typeof(EventSource), EventType.ButtonPress);

    public EventSource(EventType type)
    {
        AddProperty(TYPE);
        SetValue(TYPE, type);
    }
}
