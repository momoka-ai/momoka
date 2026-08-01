using System.Numerics;

namespace Momoka.Home.Primitives;

/// <summary>
/// 2D integer grid coordinate (X, Z). Used for XZ-plane lookups
/// such as wall occupancy, region containment, and graph node keys.
/// </summary>
public readonly record struct Int2(int X, int Z)
{
    // ── Constants ──────────────────────────────────────────
    public static readonly Int2 Zero = new(0, 0);
    public static readonly Int2 One = new(1, 1);

    // ── Conversion ─────────────────────────────────────────

    /// <summary>Explicit: drop Y from Int3.</summary>
    public static explicit operator Int2(Int3 v) => new(v.X, v.Z);

    /// <summary>Explicit: round Float3 X/Z, drop Y.</summary>
    public static explicit operator Int2(Float3 v) =>
        new((int)Math.Round(v.X), (int)Math.Round(v.Z));

    // ── Arithmetic operators ───────────────────────────────
    public static Int2 operator +(Int2 a, Int2 b) => new(a.X + b.X, a.Z + b.Z);
    public static Int2 operator -(Int2 a, Int2 b) => new(a.X - b.X, a.Z - b.Z);
    public static Int2 operator *(Int2 a, int s) => new(a.X * s, a.Z * s);
    public static Int2 operator *(int s, Int2 a) => new(a.X * s, a.Z * s);

    // ── Methods ────────────────────────────────────────────

    public Int2 Offset(int dx, int dz) => new(X + dx, Z + dz);

    public double DistanceTo(Int2 other)
    {
        var dx = X - other.X;
        var dz = Z - other.Z;
        return Math.Sqrt(dx * dx + dz * dz);
    }

    public int ManhattanDistance(Int2 other) =>
        Math.Abs(X - other.X) + Math.Abs(Z - other.Z);

    /// <summary>4-directional (von Neumann) neighbors.</summary>
    public IEnumerable<Int2> Neighbors4()
    {
        yield return new(X - 1, Z);
        yield return new(X + 1, Z);
        yield return new(X, Z - 1);
        yield return new(X, Z + 1);
    }

    /// <summary>8-directional (Moore) neighbors.</summary>
    public IEnumerable<Int2> Neighbors8()
    {
        for (var dx = -1; dx <= 1; dx++)
            for (var dz = -1; dz <= 1; dz++)
                if (dx != 0 || dz != 0)
                    yield return new(X + dx, Z + dz);
    }

    // ── To higher dimension ────────────────────────────────

    /// <summary>Lift to Int3 with given Y (default 0).</summary>
    public Int3 ToInt3(int y = 0) => new(X, y, Z);

    /// <summary>Lift to Float3 with given Y (default 0f).</summary>
    public Float3 ToFloat3(float y = 0f) => new(X, y, Z);

    /// <summary>Lift to System.Numerics.Vector3 (X,0,Z).</summary>
    public Vector3 ToVector3(float y = 0f) => new(X, y, Z);

    public override string ToString() => $"({X}, {Z})";
}
