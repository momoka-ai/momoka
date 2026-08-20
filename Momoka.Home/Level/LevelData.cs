using Momoka.Home.Entities;
using Momoka.Home.Primitives;
namespace Momoka.Home.Level;

/// <summary>
/// 家庭空间数据载荷——服务器权威模型（<see cref="ServerLevelData"/>）与客户端镜像
/// （<see cref="ClientLevelData"/>）的共同基类（Residence 的遗产）：
/// 类型 + 布局 + 全实体注册表。每服务器单存档；名称 / 地址归云端账号管理，
/// 本地仅保留档案地址（<see cref="HomeKey"/> 隐藏实体属性）。
/// </summary>
public class LevelData
{
    /// <summary>隐藏 Home 实体的键（生成存档时自动创建；无 Volume、不进空间、无编辑渠道）。</summary>
    public static readonly Key HomeKey = new("home");

    /// <summary>住宅类型（同步数据：快照分发；持久化于 Home 实体 unit_type 属性）。</summary>
    public UnitType Type { get; set; }

    /// <summary>空间布局（服务器权威；客户端重建占用镜像，不使用本字段）。</summary>
    public UnitLayout Layout { get; } = new();

    /// <summary>全实体注册表（含未放置池与 Home 实体；客户端以自身 Registry 镜像为准）。</summary>
    public List<Entity> Entities { get; } = new();

    /// <summary>
    /// 装载路径用：以存档数据整体替换当前载荷（类型 + 布局网格 + 注册表）。
    /// 服务器继承本类且 <c>EditorSession.Data</c> 恒引用自身——装载时须就地替换
    /// 而不是换新实例，否则基类字段与会话引用分裂。
    /// </summary>
    public void ReplaceWith(LevelData other)
    {
        Type = other.Type;
        Layout.Voxels = other.Layout.Voxels;
        Entities.Clear();
        Entities.AddRange(other.Entities);
    }
}
