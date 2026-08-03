using Momoka.Home;
namespace Momoka.Home.Primitives;

/// <summary>
/// Axis-aligned 3D integer bounding box defined by two corner points.
/// <see cref="Min"/> and <see cref="Max"/> are both inclusive, so
/// <c>Size = Max - Min + 1</c>. A valid bound has <c>Min &lt;= Max</c>
/// component-wise; <see cref="IsEmpty"/> indicates an invalid/unset bound.
/// </summary>
public readonly record struct Bound(Int3 Min, Int3 Max)
{
    // ── Constants ──────────────────────────────────────────
    public static readonly Bound Empty = new(Int3.Zero, Int3.Zero, isEmpty: true);

    // Backing flag distinguishes Empty from a zero-size bound at origin.
    private readonly bool _isEmpty;

    private Bound(Int3 min, Int3 max, bool isEmpty) : this(min, max)
        => _isEmpty = isEmpty;

    public bool IsEmpty => _isEmpty;

    // ── Factory ────────────────────────────────────────────

    /// <summary>Normalize two arbitrary corners into a valid min/max bound.</summary>
    public static Bound FromCorners(Int3 a, Int3 b) => new(
        new Int3(Math.Min(a.X, b.X), Math.Min(a.Y, b.Y), Math.Min(a.Z, b.Z)),
        new Int3(Math.Max(a.X, b.X), Math.Max(a.Y, b.Y), Math.Max(a.Z, b.Z)));

    /// <summary>Bound spanning from <paramref name="origin"/> for the given size.</summary>
    public static Bound FromSize(Int3 origin, Int3 size) =>
        new(origin, origin + size - Int3.One);

    /// <summary>2D XZ-plane bound (Y treated as 0).</summary>
    public static Bound FromXz(Int2 min, Int2 max) =>
        new(new Int3(min.X, 0, min.Z), new Int3(max.X, 0, max.Z));

    // ── Size ───────────────────────────────────────────────

    /// <summary>Inclusive span along X.</summary>
    public int SizeX => Max.X - Min.X + 1;
    /// <summary>Inclusive span along Y.</summary>
    public int SizeY => Max.Y - Min.Y + 1;
    /// <summary>Inclusive span along Z.</summary>
    public int SizeZ => Max.Z - Min.Z + 1;

    public Int3 Size => new(SizeX, SizeY, SizeZ);

    public Int3 Center => new(
        (Min.X + Max.X) / 2,
        (Min.Y + Max.Y) / 2,
        (Min.Z + Max.Z) / 2);

    // ── Queries ────────────────────────────────────────────

    /// <summary>Inclusive containment test.</summary>
    public bool Contains(Int3 p) =>
        p.X >= Min.X && p.X <= Max.X &&
        p.Y >= Min.Y && p.Y <= Max.Y &&
        p.Z >= Min.Z && p.Z <= Max.Z;

    /// <summary>True if <paramref name="other"/> is fully inside this bound.</summary>
    public bool Contains(Bound other) =>
        Contains(other.Min) && Contains(other.Max);

    /// <summary>True if the two bounds share at least one cell (touching counts).</summary>
    public bool Intersects(Bound other) =>
        Min.X <= other.Max.X && Max.X >= other.Min.X &&
        Min.Y <= other.Max.Y && Max.Y >= other.Min.Y &&
        Min.Z <= other.Max.Z && Max.Z >= other.Min.Z;

    // ── Set operations ─────────────────────────────────────

    /// <summary>Smallest bound containing both bounds.</summary>
    public Bound Union(Bound other) => FromCorners(
        new Int3(Math.Min(Min.X, other.Min.X), Math.Min(Min.Y, other.Min.Y), Math.Min(Min.Z, other.Min.Z)),
        new Int3(Math.Max(Max.X, other.Max.X), Math.Max(Max.Y, other.Max.Y), Math.Max(Max.Z, other.Max.Z)));

    /// <summary>Largest bound contained in both; <see cref="Empty"/> if disjoint.</summary>
    public Bound Intersect(Bound other)
    {
        var lo = new Int3(
            Math.Max(Min.X, other.Min.X),
            Math.Max(Min.Y, other.Min.Y),
            Math.Max(Min.Z, other.Min.Z));
        var hi = new Int3(
            Math.Min(Max.X, other.Max.X),
            Math.Min(Max.Y, other.Max.Y),
            Math.Min(Max.Z, other.Max.Z));
        return lo.X > hi.X || lo.Y > hi.Y || lo.Z > hi.Z
            ? Empty
            : new Bound(lo, hi);
    }

    // ── Volume ─────────────────────────────────────────────

    public long Volume => (long)SizeX * SizeY * SizeZ;

    // ── Conversion ─────────────────────────────────────────

    public override string ToString() => $"[{Min} .. {Max}]";
}
