using Momoka.Home.Data.Json;
namespace Momoka.Home.Entities;

/// <summary>A command target: the set of commands the device accepts.</summary>
[JsonTypeName("command_target")]
public class CommandTarget : Component
{
    /// <summary>JSON array of supported command names, e.g. ["turn_on","turn_off","set_temperature"].</summary>
    public string Commands { get; set; } = "[]";
}
