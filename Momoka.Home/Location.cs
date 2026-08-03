using Momoka.Home.Models.Entities;
using Momoka.Home.Primitives;

namespace Momoka.Home.Models.Levels;

/// <summary>
/// A location in the digital twin: grid coordinates plus a reference to the
/// level that owns them. Distinguishes "where" (Coords) from "which floor"
/// (Level), so a bare Int3 can never be mistaken for a full location.
/// </summary>
public readonly record struct Location(Int3 Coords, VoxelGridEntity? Composition)
{
    public int X => Coords.X;
    public int Y => Coords.Y;
    public int Z => Coords.Z;

    public override string ToString() =>
        Composition is null ? Coords.ToString() : $"{Composition}:{Coords}";
}
