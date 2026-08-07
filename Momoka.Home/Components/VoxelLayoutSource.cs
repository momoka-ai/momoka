using Momoka.Home.Layouts;
namespace Momoka.Home.Components;

/// <summary>
/// Capability component: the placement surfaces (<see cref="GridLayout{T}"/>) an
/// entity provides — wall faces, shelf boards… Attached per entity, no
/// inheritance needed; works for the generic <c>Entity&lt;T&gt;</c> types and is
/// config-driven.
/// </summary>
public class VoxelLayoutSource : Component
{
    public List<GridLayout<bool>> Layouts { get; } = new();
}
