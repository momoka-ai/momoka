using System.Text.Json.Serialization;
namespace Momoka.Home.Primitives;

/// <summary>
/// 表面姿态（transform）：位置（世界 cm）+ 旋转（<see cref="Rotation"/>，
/// 三轴欧拉 YXZ）——放置表面（<c>PlacementLayoutSource</c>）在父空间中的完整定位，
/// 直接读取 X / Y / Z / Yaw / Pitch / Roll 六个分量。无缩放（体素格网尺度由格网自身决定）。
/// 计算属性不序列化（分量由 Position/Rotation 冗余推导）。
/// </summary>
public readonly record struct Transform(Float3 Position, Rotation Rotation)
{
    /// <summary>原点朝上（水平面，法向 +Y）。</summary>
    public static readonly Transform Identity = new(Float3.Zero, Rotation.Identity);

    [JsonIgnore]
    public float X => Position.X;
    [JsonIgnore]
    public float Y => Position.Y;
    [JsonIgnore]
    public float Z => Position.Z;
    [JsonIgnore]
    public float Yaw => Rotation.Yaw;
    [JsonIgnore]
    public float Pitch => Rotation.Pitch;
    [JsonIgnore]
    public float Roll => Rotation.Roll;
    [JsonIgnore]
    public RotationAlignment RotationAlignment => Rotation.Alignment;
}
