using Momoka.Home.Layouts;
namespace Momoka.Home.Components;

/// <summary>
/// Capability component: the placement surfaces an entity provides (wall faces,
/// shelf boards…). Attached per entity — no inheritance needed, works for the
/// generic <c>Entity&lt;T&gt;</c> types, and config-driven (templates declare it).
/// </summary>
public class SurfaceSource : Component
{
    public List<VoxelLayout2D> Layouts { get; } = new();
}
