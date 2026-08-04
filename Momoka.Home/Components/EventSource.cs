namespace Momoka.Home.Components;

public enum EventType
{
    ButtonPress,
    MotionDetected,
    ContactChanged,
    ThresholdReached
}

/// <summary>An event source: a typed carrier of the event kind.</summary>
public class EventSource : Component
{
    public EventType Type { get; set; }

    public EventSource(EventType type)
    {
        Type = type;
    }
}
