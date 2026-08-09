namespace Momoka.Home.Primitives;

/// <summary>
/// Axis-aligned 3D bounding box in continuous world units (cm), defined by two
/// corner points (<see cref="Min"/>, <see cref="Max"/>). A bound is
/// <see cref="IsValid"/> iff both corners lie inside the world extent
/// (±<see cref="MAXIMUM"/>) and <see cref="Min"/> ≤ <see cref="Max"/>
/// component-wise. The default value and <see cref="Invalid"/> are out-of-range
/// sentinels meaning "unset" — validity is derived from the value itself (no
/// backing flag), so the struct round-trips through JSON as { min, max }.
/// </summary>
public readonly record struct Bound(Float3 Min, Float3 Max)
{
    /// <summary>World extent bounds in cells (±16384 = 1024 chunks of 16) — any coordinate beyond these marks a bound invalid/unset.</summary>
    public static readonly Float3 MAXIMUM = new(16384.0f);
    public static readonly Float3 MINIMUM = new(-16384.0f);

    /// <summary>Out-of-range sentinel meaning "unset / invalid".</summary>
    public static readonly Bound Invalid = new(
        new Float3(float.MaxValue, float.MaxValue, float.MaxValue),
        new Float3(float.MinValue, float.MinValue, float.MinValue));

    /// <summary>Two integer corners (cell units), converted to world units.</summary>
    public Bound(Int3 min, Int3 max) : this(min.ToFloat3(), max.ToFloat3()) { }

    /// <summary>Two integer XZ corners — a flat XZ-plane bound (Y treated as 0).</summary>
    public Bound(Int2 min, Int2 max)
        : this(new Float3(min.X, 0, min.Z), new Float3(max.X, 0, max.Z)) { }

    /// <summary>Six explicit corner components.</summary>
    public Bound(float minX, float minY, float minZ, float maxX, float maxY, float maxZ)
        : this(new Float3(minX, minY, minZ), new Float3(maxX, maxY, maxZ)) { }

    /// <summary>True when both corners lie within ±16384 (see <see cref="MAXIMUM"/>/<see cref="MINIMUM"/>) and Min ≤ Max (component-wise).</summary>
    public bool IsValid =>
        Min >= MINIMUM && Min <= MAXIMUM &&
        Max >= MINIMUM && Max <= MAXIMUM &&
        Min <= Max;

    /// <summary>Normalize two arbitrary corners into a valid min/max bound.</summary>
    public static Bound FromCorners(Float3 a, Float3 b) => new(
        new Float3(Math.Min(a.X, b.X), Math.Min(a.Y, b.Y), Math.Min(a.Z, b.Z)),
        new Float3(Math.Max(a.X, b.X), Math.Max(a.Y, b.Y), Math.Max(a.Z, b.Z)));

    // ── Size ───────────────────────────────────────────────

    /// <summary>Inclusive span along X.</summary>
    public float SizeX => Max.X - Min.X + 1;
    /// <summary>Inclusive span along Y.</summary>
    public float SizeY => Max.Y - Min.Y + 1;
    /// <summary>Inclusive span along Z.</summary>
    public float SizeZ => Max.Z - Min.Z + 1;

    public Float3 Size => new(SizeX, SizeY, SizeZ);

    public Float3 Center => new(
        (Min.X + Max.X) / 2,
        (Min.Y + Max.Y) / 2,
        (Min.Z + Max.Z) / 2);

    // ── Queries ────────────────────────────────────────────

    /// <summary>Inclusive containment test.</summary>
    public bool Contains(Float3 p) =>
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
        new Float3(Math.Min(Min.X, other.Min.X), Math.Min(Min.Y, other.Min.Y), Math.Min(Min.Z, other.Min.Z)),
        new Float3(Math.Max(Max.X, other.Max.X), Math.Max(Max.Y, other.Max.Y), Math.Max(Max.Z, other.Max.Z)));

    /// <summary>Largest bound contained in both; <see cref="Invalid"/> if disjoint.</summary>
    public Bound Intersect(Bound other)
    {
        var lo = new Float3(
            Math.Max(Min.X, other.Min.X),
            Math.Max(Min.Y, other.Min.Y),
            Math.Max(Min.Z, other.Min.Z));
        var hi = new Float3(
            Math.Min(Max.X, other.Max.X),
            Math.Min(Max.Y, other.Max.Y),
            Math.Min(Max.Z, other.Max.Z));
        return lo.X > hi.X || lo.Y > hi.Y || lo.Z > hi.Z
            ? Invalid
            : new Bound(lo, hi);
    }

    // ── Volume ─────────────────────────────────────────────

    public float Volume => SizeX * SizeY * SizeZ;

    // ── Conversion ─────────────────────────────────────────

    public override string ToString() => $"[{Min} .. {Max}]";
}
