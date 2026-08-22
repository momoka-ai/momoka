using Momoka.Home.Data.Json;
namespace Momoka.Home.Levels.Entities.Components;

public enum EventType
{
    ButtonPress,
    MotionDetected,
    ContactChanged,
    ThresholdReached
}

/// <summary>An event source: a typed carrier of the event kind.</summary>
[JsonTypeName("event_source")]
public class EventSource : Component
{
    public EventType Type { get; set; }

    public EventSource() { }

    public EventSource(EventType type)
    {
        Type = type;
    }
}
