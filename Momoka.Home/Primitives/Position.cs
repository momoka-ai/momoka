using System.Text.Json.Serialization;
namespace Momoka.Home.Primitives;

/// <summary>
/// A 3D position carrying its own unit scale: the stored <see cref="Pos"/>
/// vector is expressed in units where one unit equals <see cref="Scale"/>
/// centimeters (<c>Scale = 1</c> → absolute cm; voxel indices ride along as
/// <c>Scale = 10</c>). Because the scale travels with the value, any position
/// is self-describing — <see cref="Absolute"/> always yields the real cm
/// coordinate no matter how the value was produced, and <see cref="Rescale"/>
/// re-expresses the same point in another unit.
/// </summary>
public readonly record struct Position(Float3 Pos, float Scale = 1f)
{
    public static readonly Position Zero = new(Float3.Zero);

    public Position(Int3 cell, float scale) : this(cell.ToFloat3(), scale) { }

    [JsonIgnore]
    public float X => Pos.X;

    [JsonIgnore]
    public float Y => Pos.Y;

    [JsonIgnore]
    public float Z => Pos.Z;

    [JsonIgnore]
    public Float3 Normalized => Pos * Scale;

    public Float3 AsFloat3() => Pos;

    public Int3 AsInt3() => new(
        (int)Math.Round(Pos.X, MidpointRounding.AwayFromZero),
        (int)Math.Round(Pos.Y, MidpointRounding.AwayFromZero),
        (int)Math.Round(Pos.Z, MidpointRounding.AwayFromZero));

    public Float3 Absolute() => Pos * Scale;

    public Position Rescale(float scale) => new(Pos * (Scale / scale), scale);

    public static Position operator +(Position a, Float3 offset) => new(a.Pos + offset, a.Scale);

    public static Position operator -(Position a, Float3 offset) => new(a.Pos - offset, a.Scale);

    public static Position operator -(Position a, Position b) =>
        new(a.Absolute() - b.Absolute(), 1f);

    public bool Equals(Position other) => Absolute() == other.Absolute();

    public override int GetHashCode() => Absolute().GetHashCode();
}
