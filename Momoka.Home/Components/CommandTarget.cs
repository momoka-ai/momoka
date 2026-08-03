using Momoka.Home;
using Momoka.Home.Primitives;
using Momoka.Home.States;
namespace Momoka.Home.Components;

public class CommandTarget : Component
{
    public static readonly StringProperty COMMANDS = new("commands", new Key("commandtarget"),
        description: "JSON array of supported command names, e.g. [\"turn_on\",\"turn_off\",\"set_temperature\"]");

    public CommandTarget()
    {
        AddProperty(COMMANDS);
        SetValue(COMMANDS, "[]");
    }
}
