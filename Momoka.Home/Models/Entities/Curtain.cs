using Momoka.Home.Models.States;

namespace Momoka.Home.Models.Entities;

public class Curtain : Appliance
{
    public static readonly FloatProperty POSITION = new("position", typeof(Curtain), 0f,
        description: "Curtain openness, 0 = fully closed, 100 = fully open");

    public Curtain()
    {
        AddProperty(POSITION);
    }
}
