using Newtonsoft.Json;
namespace Momoka.Home.Primitives;

/// <summary>
/// 平面朝向：yaw（绕 Y 轴，度）+ pitch（俯仰，度）——单一平面法向的 2 参数
/// 参数化（平面法向无自转自由度，故不需要 roll）。预定义 6 向与
/// <see cref="Int3"/> 的 Up/Down/East/West/North/South 一一对应。
/// **姿态表达已由 <see cref="Rotation"/>（三轴欧拉 YXZ，含 roll）取代**——
/// 本类型现仅用于 <c>Entity.ContactFace</c>（物件接触面声明，体素物件 6 向，
/// 恒轴对齐）；表面姿态统一用 <see cref="Rotation"/>。
/// </summary>
public readonly record struct Direction(float Yaw, float Pitch)
{
    /// <summary>法向 +Y（水平面朝上，如地板顶面）。</summary>
    public static readonly Direction Up = new(0, 0);
    /// <summary>法向 −Y（水平面朝下，如天花板底面）。</summary>
    public static readonly Direction Down = new(0, 180);
    /// <summary>法向 +Z（与 <see cref="Int3.North"/> 一致）。</summary>
    public static readonly Direction North = new(0, 90);
    /// <summary>法向 −Z（与 <see cref="Int3.South"/> 一致）。</summary>
    public static readonly Direction South = new(180, 90);
    /// <summary>法向 +X（与 <see cref="Int3.East"/> 一致）。</summary>
    public static readonly Direction East = new(90, 90);
    /// <summary>法向 −X（与 <see cref="Int3.West"/> 一致）。</summary>
    public static readonly Direction West = new(-90, 90);

    /// <summary>示例斜表面：45° 俯仰（坡屋顶面）。</summary>
    public static readonly Direction Roof45 = new(0, 45);

    /// <summary>法向单位向量：Ry(yaw)·Rx(pitch)·(0,1,0)。
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

    /// <summary>表面行轴（格网 x 方向，仅随 yaw 旋转）：Ry(yaw)·(1,0,0)。
    /// 决定物件"放出来"沿表面的朝向。</summary>
    [JsonIgnore]
    public Float3 RowAxis
    {
        get
        {
            var (sy, cy) = Math.SinCos(Yaw * Math.PI / 180);
            return new Float3((float)cy, 0, (float)(-sy));
        }
    }

    /// <summary>表面列轴（格网 z 方向）：行轴 × 法向（右手系，已单位）。
    /// 该方向约定保证 Up 面（yaw=0, pitch=0）的列轴沿 +Z，与既有的
    /// "Up 面 AsAbsolute(rel) = Offset + (rel.X, 0, rel.Z)" 行为一致。</summary>
    [JsonIgnore]
    public Float3 ColumnAxis => Float3.Cross(RowAxis, Normal);

    /// <summary>是否轴对齐表面（yaw/pitch 均为 90° 整数倍的容差内）——
    /// 当前体素放置能力只支持轴对齐表面，斜表面为描述预留。</summary>
    [JsonIgnore]
    public bool IsAxisAligned =>
        Math.Abs(Yaw % 90) < 1e-3f && Math.Abs(Pitch % 90) < 1e-3f;

    /// <summary>朝向类别：按法向 Y 分量分类（<see cref="DirectionAlignment"/>）——
    /// 法向水平 = Vertical、竖直 = Upside/Downside（按符号）、其余 = Tilted。
    /// 用于物件的"期望表面类型"校验。</summary>
    [JsonIgnore]
    public DirectionAlignment Alignment
    {
        get
        {
            var y = Normal.Y;
            if (Math.Abs(y) < 1e-3f)
                return DirectionAlignment.Vertical;
            if (Math.Abs(Math.Abs(y) - 1) < 1e-3f)
                return y > 0 ? DirectionAlignment.Upside : DirectionAlignment.Downside;
            return DirectionAlignment.Tilted;
        }
    }

    /// <summary>法向相反的方向（贴合判定用：物件接触面法向须与宿主表面法向相反）。
    /// yaw 规范到 [-180, 180)：Up↔Down、North↔South、East↔West 几何互反
    /// （East=(90,90) 的反向是 (-90,90)=West 而非 (270,90)）。
    /// 计算属性不序列化（自递归）。</summary>
    [JsonIgnore]
    public Direction Opposite
    {
        get
        {
            var yaw = (Yaw + 180) % 360;
            if (yaw >= 180) yaw -= 360;
            if (yaw < -180) yaw += 360;
            return new(yaw, 180 - Pitch);
        }
    }
}
