using Momoka.Home;
using Momoka.Home.Entities;
using Momoka.Home.Primitives;
using Momoka.Home.States;
namespace Momoka.Home.Components;

/// <summary>
/// Base for behavior components attached to an <see cref="IComponentSource"/>.
/// A component is a pure property carrier (source id, type, value...) — it is
/// not an <see cref="Entity"/>, so it cannot host components or participate in
/// the spatial model.
/// </summary>
public abstract class Component : PropertyValueObject
{
    public static readonly StringProperty SOURCE_ID = new("source_id", new Key("component"));

    public Guid Id { get; init; } = Guid.NewGuid();

    protected Component()
    {
        AddProperty(SOURCE_ID);
    }
}
