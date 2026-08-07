namespace Momoka.Home.Components;

/// <summary>
/// Capability of an object to hold behavior components — pure property carriers
/// (data sources, event sources, command targets...) attached to the host.
/// Implementers only expose the component list; all queries and mutations live
/// in <see cref="ComponentSourceExtensions"/>.
/// </summary>
public interface IComponentSource
{
    IList<Component> Components { get; }
}
