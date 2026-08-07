using Momoka.Home.Layouts;
namespace Momoka.Home.Components;

/// <summary>
/// Capability component: one placement surface (<see cref="GridLayout{T}"/>) an
/// entity provides — a floor slab's top face, a shelf board, a stair tread…
/// Attach multiple instances for objects with several surfaces (bookshelves,
/// stairs). Config-driven.
/// </summary>
public class PlacementLayoutSource : Component
{
    /// <summary>The single placement surface this component provides, or null.</summary>
    public GridLayout<bool>? Layout { get; set; }
}
