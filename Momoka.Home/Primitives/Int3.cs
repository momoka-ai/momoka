using System.Numerics;

namespace Momoka.Home.Primitives;

/// <summary>
/// 3D integer grid coordinate (X, Y, Z). Represents a discrete position
/// at 10 cm precision. Used as a dictionary key in BlockCanvas.
/// </summary>
public readonly record struct Int3(int X, int Y, int Z)
{
    // ── Constants ──────────────────────────────────────────
    public static readonly Int3 Zero = new(0, 0, 0);
    public static readonly Int3 One = new(1, 1, 1);
    public static readonly Int3 Up = new(0, 1, 0);
    public static readonly Int3 Down = new(0, -1, 0);

    // ── Conversion ─────────────────────────────────────────

    /// <summary>Explicit: round Float3.</summary>
    public static explicit operator Int3(Float3 v) =>
        new((int)Math.Round(v.X), (int)Math.Round(v.Y), (int)Math.Round(v.Z));

    // ── Arithmetic operators ───────────────────────────────
    public static Int3 operator +(Int3 a, Int3 b) =>
        new(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
    public static Int3 operator -(Int3 a, Int3 b) =>
        new(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
    public static Int3 operator *(Int3 a, int s) =>
        new(a.X * s, a.Y * s, a.Z * s);
    public static Int3 operator *(int s, Int3 a) => a * s;
    public static Int3 operator %(Int3 a, int s) => new(a.X % s, a.Y % s, a.Z % s);
    public static Int3 operator %(Int3 a, Int3 b) => new(a.X % b.X, a.Y % b.Y, a.Z % b.Z);

    // ── ValueTuple decomposition ───────────────────────────

    /// <summary>Drop to XZ-plane (Int2).</summary>
    public Int2 Xz => new(X, Z);

    // ── Methods ────────────────────────────────────────────

    public Int3 Offset(int dx, int dy, int dz) =>
        new(X + dx, Y + dy, Z + dz);

    public double DistanceTo(Int3 other)
    {
        var dx = X - other.X;
        var dy = Y - other.Y;
        var dz = Z - other.Z;
        return Math.Sqrt(dx * dx + dy * dy + dz * dz);
    }

    public int ManhattanDistance(Int3 other) =>
        Math.Abs(X - other.X) + Math.Abs(Y - other.Y) + Math.Abs(Z - other.Z);

    /// <summary>6-directional (face-adjacent) neighbors.</summary>
    public IEnumerable<Int3> Neighbors6()
    {
        yield return new(X - 1, Y, Z);
        yield return new(X + 1, Y, Z);
        yield return new(X, Y - 1, Z);
        yield return new(X, Y + 1, Z);
        yield return new(X, Y, Z - 1);
        yield return new(X, Y, Z + 1);
    }

    // ── To float / Vector3 ─────────────────────────────────

    /// <summary>Convert to Float3.</summary>
    public Float3 ToFloat3() => new(X, Y, Z);

    /// <summary>Convert to System.Numerics.Vector3.</summary>
    public Vector3 ToVector3() => new(X, Y, Z);

    public override string ToString() => $"({X}, {Y}, {Z})";
}
