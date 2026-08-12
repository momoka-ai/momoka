namespace Momoka.Home;

/// <summary>
/// A mobile agent's movement attributes — the unit that walks, climbs and
/// navigates the space (a person, a robot, a pet). All values are in cells
/// (10 cm each); defaults are human. Region connectivity tolerances and future
/// pathfinding derive from these (e.g. <see cref="MaxClimbHeight"/> is the max
/// step between adjacent columns' spans).
/// </summary>
public record class Agent(
    float Height,         // 标准身高 ≈ 1.8 m —— 通行净高 / 高物阻隔阈值
    float Radius,          // 半径 ≈ 40 cm —— 通道宽度
    float MaxClimbHeight,  // 爬升高度 ≈ 20 cm —— 台阶 / 邻列 span 连通容差
    float MaxJumpHeight,   // 跳跃高度 ≈ 60 cm
    float MaxWalkLength,   // 最大单次步行距离 ≈ 3 m（寻路启发式）
    float MaxInteractLength)  // 最大触及距离
{
    /// <summary>The default human agent.</summary>
    public static Agent Human { get; } = new(180f, 40f, 20f, 60f, 80f, 60f);
}
