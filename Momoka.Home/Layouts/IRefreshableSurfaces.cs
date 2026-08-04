namespace Momoka.Home.Layouts;

/// <summary>
/// An entity whose placement surfaces are derived from its current geometry
/// (e.g. a wall's faces) and must be re-materialized into its
/// <see cref="SurfaceSource"/> component after the geometry changes.
/// </summary>
public interface IRefreshableSurfaces
{
    void RefreshSurfaces();
}
