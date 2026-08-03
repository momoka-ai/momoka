using Momoka.Home;
using Momoka.Home.Primitives;
using Momoka.Home.States;
namespace Momoka.Home.Components;

public enum EventType
{
    ButtonPress,
    MotionDetected,
    ContactChanged,
    ThresholdReached
}

public class EventSource : Component
{
    public static readonly EnumProperty<EventType> TYPE = new("event_type", new Key("eventsource"), EventType.ButtonPress);

    public EventSource(EventType type)
    {
        AddProperty(TYPE);
        SetValue(TYPE, type);
    }
}
