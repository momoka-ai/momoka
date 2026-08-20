using Newtonsoft.Json;
namespace Momoka.Home.Primitives;

/// <summary>
/// 完整姿态旋转：yaw（绕 Y 轴）+ pitch（绕 X 轴）+ roll（绕 Z 轴），单位度，
/// **内旋 YXZ 顺序**（先 yaw 再 pitch 再 roll）——与 Godot 的默认欧拉约定
/// （<c>Basis.from_euler(Vector3(pitch, yaw, roll), EULER_ORDER_YXZ)</c>）完全一致，
/// 渲染端（Momoka.Ui / Godot）零转换直接映射（弧度转换见 <see cref="ToGodotRadians"/>）。
/// 3 自由度覆盖任意姿态；yaw+pitch 特例（roll=0）即平面表面朝向（2 参数），
/// roll 表达绕法向的自转（如挂画歪挂、物件在表面上的朝向）。
/// </summary>
public readonly record struct Rotation(float Yaw, float Pitch, float Roll)
{
    /// <summary>零旋转（局部 +Y 朝世界 +Y，行轴沿 +X）。</summary>
    public static readonly Rotation Identity = new(0, 0, 0);

    /// <summary>法向 +Y（水平面朝上，如地板顶面）——即零旋转。</summary>
    public static readonly Rotation Up = Identity;
    /// <summary>法向 −Y（水平面朝下，如天花板底面）。</summary>
    public static readonly Rotation Down = new(0, 180, 0);
    /// <summary>法向 +Z（与 <see cref="Int3.North"/> 一致）。</summary>
    public static readonly Rotation North = new(0, 90, 0);
    /// <summary>法向 −Z（与 <see cref="Int3.South"/> 一致）。</summary>
    public static readonly Rotation South = new(180, 90, 0);
    /// <summary>法向 +X（与 <see cref="Int3.East"/> 一致）。</summary>
    public static readonly Rotation East = new(90, 90, 0);
    /// <summary>法向 −X（与 <see cref="Int3.West"/> 一致）。</summary>
    public static readonly Rotation West = new(-90, 90, 0);

    /// <summary>示例斜姿态：45° 俯仰（坡屋顶面法向）。</summary>
    public static readonly Rotation Roof45 = new(0, 45, 0);

    /// <summary>法向单位向量：Ry(yaw)·Rx(pitch)·Rz(roll)·(0,1,0)——roll 不改变
    /// 法向（绕局部 Z 自转），故与 yaw+pitch 的平面朝向公式一致。
    /// 轴对齐特例：Up=(0,1,0)、Down=(0,−1,0)、East=(1,0,0)、West=(−1,0,0)、
    /// North=(0,0,1)、South=(0,0,−1)。</summary>
    [JsonIgnore]
    public Float3 Normal
    {
        get
        {
            var (sy, cy) = Math.SinCos(Yaw * Math.PI / 180);
            var (sp, cp) = Math.SinCos(Pitch * Math.PI / 180);
            return new Float3((float)(sp * sy), (float)cp, (float)(sp * cy));
        }
    }

    /// <summary>表面行轴（局部 +X 经旋转后）：Ry(yaw)·Rx(pitch)·Rz(roll)·(1,0,0)——
    /// 决定物件"放出来"沿表面的朝向；roll=0 时退化为仅随 yaw 旋转的平面行轴。</summary>
    [JsonIgnore]
    public Float3 RowAxis
    {
        get
        {
            var (sr, cr) = Math.SinCos(Roll * Math.PI / 180);
            var (sy, cy) = Math.SinCos(Yaw * Math.PI / 180);
            var (sp, cp) = Math.SinCos(Pitch * Math.PI / 180);
            return new Float3(
                (float)(cr * cy + sp * sr * sy),
                (float)(cp * sr),
                (float)(-cr * sy + sp * sr * cy));
        }
    }

    /// <summary>表面列轴（局部 +Z 经旋转后）：Ry(yaw)·Rx(pitch)·(0,0,1)——roll
    /// 是绕列轴的自转，不改变列轴。Up 面（Identity）下列轴沿 +Z——与
    /// "Up 面 AsAbsolute(rel) = Position/UnitLength + (rel.X, 0, rel.Z)" 行为一致。
    /// 注意：不能用行轴 × 法向（Cross）——roll=90° 时行轴与法向平行，Cross 退化为零。</summary>
    [JsonIgnore]
    public Float3 ColumnAxis
    {
        get
        {
            var (sy, cy) = Math.SinCos(Yaw * Math.PI / 180);
            var (sp, cp) = Math.SinCos(Pitch * Math.PI / 180);
            return new Float3((float)(cp * sy), (float)(-sp), (float)(cp * cy));
        }
    }

    /// <summary>是否轴对齐姿态（yaw/pitch/roll 均为 90° 整数倍的容差内）——
    /// 当前体素放置能力只支持轴对齐表面，斜姿态为描述预留。</summary>
    [JsonIgnore]
    public bool IsAxisAligned =>
        Math.Abs(Yaw % 90) < 1e-3f && Math.Abs(Pitch % 90) < 1e-3f && Math.Abs(Roll % 90) < 1e-3f;

    /// <summary>姿态类别：按法向 Y 分量分类（<see cref="RotationAlignment"/>）——
    /// 法向水平 = Vertical、竖直 = Upside/Downside（按符号）、其余 = Tilted。
    /// 用于物件的"期望表面类型"校验。</summary>
    [JsonIgnore]
    public RotationAlignment Alignment
    {
        get
        {
            var y = Normal.Y;
            if (Math.Abs(y) < 1e-3f)
                return RotationAlignment.Vertical;
            if (Math.Abs(Math.Abs(y) - 1) < 1e-3f)
                return y > 0 ? RotationAlignment.Upside : RotationAlignment.Downside;
            return RotationAlignment.Tilted;
        }
    }

    /// <summary>Godot 欧拉映射（弧度）：Vector3(x=pitch, y=yaw, z=roll)——
    /// Godot <c>Basis.from_euler</c> 的默认 YXZ 内旋顺序与本类型一致，
    /// 直接赋给 Node3D.Rotation 即可。</summary>
    public Float3 ToGodotRadians() => new(Pitch * MathF.PI / 180, Yaw * MathF.PI / 180, Roll * MathF.PI / 180);

    /// <summary>从 Godot 欧拉（弧度，Vector3(x=pitch, y=yaw, z=roll)）构造。</summary>
    public static Rotation FromGodotRadians(Float3 godot) => new(
        godot.Y * 180 / MathF.PI,
        godot.X * 180 / MathF.PI,
        godot.Z * 180 / MathF.PI);
}
