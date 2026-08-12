namespace Momoka.Home.Algorithms;

/// <summary>
/// Sight-blocking policy for ray-based queries: what kind of cell stops a ray,
/// ordered from most permissive to most restrictive.
/// </summary>
public enum Occlusion
{
    None,
    OnlyImmutable,
    OnlyNonTransparent,
    Everything,
}
