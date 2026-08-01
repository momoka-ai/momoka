using Momoka.Home.Primitives;

namespace Momoka.Home.Models.Interfaces;

/// <summary>
/// Capability of a space whose floor area is partitioned into named polygon
/// regions (living room, lawn, driveway...). Regions are the faces bounded by
/// the <see cref="IWallGraph"/> topology — they should fill the floor and not
/// intersect any wall.
/// </summary>
public interface IRegionLayout
{
    List<Region> Regions { get; }
}

public static class RegionLayoutExtensions
{
    /// <summary>Returns a copy of all regions in this space.</summary>
    public static List<Region> ListRegions(this IRegionLayout layout) => new(layout.Regions);

    /// <summary>Returns the first region whose polygon boundary contains the given XZ point, or null.</summary>
    public static Region? GetRegion(this IRegionLayout layout, Int2 point) =>
        layout.Regions.FirstOrDefault(r => r.Contains(point));

    /// <summary>Returns the region with the given name (case-sensitive), or null.</summary>
    public static Region? GetRegion(this IRegionLayout layout, string name) =>
        layout.Regions.FirstOrDefault(r => r.Name == name);

    /// <summary>Creates a new empty region with the given name and adds it to this space.</summary>
    public static Region AddRegion(this IRegionLayout layout, string name)
    {
        var region = new Region { Name = name };
        layout.Regions.Add(region);
        return region;
    }

    /// <summary>
    /// Adds a region with the given name if none exists yet.
    /// Returns the existing region, or null if a new one was created.
    /// </summary>
    public static Region? AddRegionIfAbsent(this IRegionLayout layout, string name)
    {
        var existing = layout.GetRegion(name);
        if (existing is not null) return null;
        return layout.AddRegion(name);
    }

    /// <summary>Removes the specified region from this space.</summary>
    public static void RemoveRegion(this IRegionLayout layout, Region region) =>
        layout.Regions.Remove(region);

    /// <summary>Removes the region with the given name, if it exists.</summary>
    public static void RemoveRegion(this IRegionLayout layout, string name)
    {
        var region = layout.GetRegion(name);
        if (region is not null) layout.Regions.Remove(region);
    }

    /// <summary>
    /// Merges the boundaries of two adjacent regions into a single combined region.
    /// The second region is removed after merging.
    /// Returns the merged region, or null if the polygons cannot be combined.
    /// </summary>
    public static Region? TryCombineRegion(this IRegionLayout layout, Region r1, Region r2)
    {
        // TODO: Requires polygon union implementation (convex hull or path merge).
        // For now, returns null to indicate "not yet implemented".
        return null;
    }
}