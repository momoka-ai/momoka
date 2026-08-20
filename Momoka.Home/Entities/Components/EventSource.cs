using Momoka.Home.Data.Json;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
namespace Momoka.Home.Entities.Components;

[JsonConverter(typeof(StringEnumConverter))]
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
