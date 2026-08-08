using Momoka.Home.Entities;
using Momoka.Home.Layouts;
namespace Momoka.Home.Storage;

/// <summary>
/// Builds a live <see cref="Residence"/> from a fully-loaded <see cref="Save"/>:
/// the reconstructed voxel grid + region layer are injected into the residence's
/// unit layout and its entity list is repopulated. The caller must load the save
/// via <see cref="SaveStore.Load"/> first — metadata-only saves from
/// <see cref="SaveStore.ListSaves"/> carry no grid.
/// </summary>
public static class SaveFactory
{
    public static Residence BuildResidence(Save save)
    {
        if (save.Grid is null)
        {
            throw new InvalidOperationException(
                $"Save '{save.Name}' has no voxel grid — load it via SaveStore.Load before building.");
        }

        var residence = new Residence
        {
            Name = save.Name,
            Address = save.Address,
            Type = save.Type,
        };
        residence.Layout.Restore(save.Grid, save.Entities.Values, save.Regions);
        return residence;
    }
}
