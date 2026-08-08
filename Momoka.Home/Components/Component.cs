namespace Momoka.Home.Components;

/// <summary>
/// A behavior component attached to an <see cref="IComponentSource"/> — a pure,
/// typed behavior carrier (data source, event source, command target…). Not a
/// property-value holder: it carries its own typed fields.
/// </summary>
public abstract class Component
{
    public Guid Id { get; init; } = Guid.NewGuid();

    /// <summary>Identity of the integration/source this component binds to.</summary>
    public string SourceId { get; set; } = "";
}

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
