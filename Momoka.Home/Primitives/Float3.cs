using System.Numerics;
using System.Text.Json.Serialization;
using Momoka.Home;
namespace Momoka.Home.Primitives;

/// <summary>
/// 3D continuous coordinate (X, Y, Z). Used for precise positioning
/// of mobile entities (humans, pets, robots) as well as shape vertices.
/// </summary>
public readonly record struct Float3(float X, float Y, float Z)
{
    public Float3(float length) : this(length, length, length) { }

    // ── Constants ──────────────────────────────────────────
    public static readonly Float3 Zero = new(0f, 0f, 0f);
    public static readonly Float3 One = new(1f, 1f, 1f);
    public static readonly Float3 Up = new(0f, 1f, 0f);
    public static readonly Float3 Down = new(0f, -1f, 0f);

    // ── Conversion ─────────────────────────────────────────
    // Conversions are explicit only — no implicit conversion between
    // primitive types (use ToInt3/ToFloat3/ToVector3 or the Xz/Int2/Int3 props).

    /// <summary>From System.Numerics.Vector3.</summary>
    public static Float3 FromVector3(Vector3 v) => new(v.X, v.Y, v.Z);

    /// <summary>To System.Numerics.Vector3.</summary>
    public Vector3 ToVector3() => new(X, Y, Z);

    // ── Arithmetic operators ───────────────────────────────
    public static Float3 operator +(Float3 a, Float3 b) =>
        new(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
    public static Float3 operator -(Float3 a, Float3 b) =>
        new(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
    public static Float3 operator *(Float3 a, float s) =>
        new(a.X * s, a.Y * s, a.Z * s);
    public static Float3 operator *(float s, Float3 a) => a * s;
    public static Float3 operator /(Float3 a, float s) =>
        new(a.X / s, a.Y / s, a.Z / s);
    public static Float3 operator -(Float3 a) =>
        new(-a.X, -a.Y, -a.Z);

    // ── Comparison operators ──────────────────────────────
    // Component-wise, not a total order: a <= b means EVERY component of a is
    // <= the matching component of b (axis-aligned box containment).

    public static bool operator <(Float3 a, Float3 b) =>
        a.X < b.X && a.Y < b.Y && a.Z < b.Z;

    public static bool operator <=(Float3 a, Float3 b) =>
        a.X <= b.X && a.Y <= b.Y && a.Z <= b.Z;

    public static bool operator >(Float3 a, Float3 b) =>
        a.X > b.X && a.Y > b.Y && a.Z > b.Z;

    public static bool operator >=(Float3 a, Float3 b) =>
        a.X >= b.X && a.Y >= b.Y && a.Z >= b.Z;


    /// <summary>Floor to Int3 (truncate toward zero).</summary>
    [JsonIgnore]
    public Int3 Int3Floor =>
        new((int)X, (int)Y, (int)Z);

    [JsonIgnore]
    public float Magnitude => MathF.Sqrt(X * X + Y * Y + Z * Z);

    [JsonIgnore]
    public Float3 Normalized => Magnitude > 1e-6f ? this / Magnitude : Zero;

    // ── Methods ────────────────────────────────────────────

    public Float3 Offset(float dx, float dy, float dz) =>
        new(X + dx, Y + dy, Z + dz);

    public double DistanceTo(Float3 other)
    {
        var dx = X - other.X;
        var dy = Y - other.Y;
        var dz = Z - other.Z;
        return Math.Sqrt(dx * dx + dy * dy + dz * dz);
    }

    public static Float3 Lerp(Float3 a, Float3 b, float t) =>
        new(
            a.X + (b.X - a.X) * t,
            a.Y + (b.Y - a.Y) * t,
            a.Z + (b.Z - a.Z) * t
        );

    public static float Dot(Float3 a, Float3 b) =>
        a.X * b.X + a.Y * b.Y + a.Z * b.Z;

    public static Float3 Cross(Float3 a, Float3 b) =>
        new(
            a.Y * b.Z - a.Z * b.Y,
            a.Z * b.X - a.X * b.Z,
            a.X * b.Y - a.Y * b.X
        );

    /// <summary>Grid-snapped at 10 cm (1 unit) resolution.</summary>
    public Float3 SnapToGrid(float gridSize = 1f) =>
        new(
            MathF.Round(X / gridSize) * gridSize,
            MathF.Round(Y / gridSize) * gridSize,
            MathF.Round(Z / gridSize) * gridSize
        );

    public Int2 AsInt2() => new(
        (int)Math.Round(X),
        (int)Math.Round(Z));

    public Int3 AsInt3() => new(
        (int)Math.Round(X),
        (int)Math.Round(Y),
        (int)Math.Round(Z));

    public Int3 AsInt3F() => new(
        (int)X,
        (int)Y,
        (int)Z);

    public override string ToString() =>
        $"({X:F3}, {Y:F3}, {Z:F3})";
}
