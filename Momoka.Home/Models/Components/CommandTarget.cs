using Momoka.Home.Models.States;

namespace Momoka.Home.Models.Components;

public class CommandTarget : Component
{
    public static readonly StringProperty COMMANDS = new("commands", typeof(CommandTarget),
        description: "JSON array of supported command names, e.g. [\"turn_on\",\"turn_off\",\"set_temperature\"]");

    public CommandTarget()
    {
        AddProperty(COMMANDS);
        SetValue(COMMANDS, "[]");
    }
}
