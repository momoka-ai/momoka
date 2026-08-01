using Momoka.Home.Models.States;

namespace Momoka.Home.Models.Entities;

public abstract class Component : Entity
{
    public static readonly StringProperty SOURCE_ID = new("source_id", typeof(Component));

    protected Component()
    {
        AddProperty(SOURCE_ID);
    }
}
