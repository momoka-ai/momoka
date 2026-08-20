using Momoka.Home.Entities;
using Momoka.Home.Primitives;
namespace Momoka.Home.Editing;

/// <summary>变更种类：新增（含从未放置池创建并放置）/ 移除 / 修改。</summary>
public enum EntityChangeKind
{
    Added,
    Removed,
    Modified,
}

/// <summary>
/// 单条实体变更：<see cref="Entity"/> 为变更后的实体引用（removed 时即被移除的实体，
/// 凭其 Id 即可）。**不含旧值快照**——协议线 <c>layout_changed</c> 只带
/// <see cref="Protocol.EntityDelta"/>（无 Old），客户端凭本地 Registry 推导旧值。
/// </summary>
public readonly record struct EntityChange(EntityChangeKind Kind, Entity Entity);

/// <summary>
/// 一次编辑操作产生的变更记录（**模型内部契约**，不上协议线）：命令执行 / 撤销 /
/// 重做后由 <see cref="EditorSession"/> 统一组装——实体增改删列表 + 受影响 Region 集
/// （<see cref="RegionMaintainer"/> 增量输出，<c>region_changed</c> 帧预留用）。
/// 脏区块不在服务端计算——客户端由 <see cref="ClientLevelData"/> 凭实体旧/新格本地推导。
/// </summary>
public sealed class ChangeSet
{
    public List<EntityChange> Changes { get; } = new();
    public HashSet<int> AffectedRegions { get; } = new();

    public ChangeSet Added(Entity entity)
    {
        Changes.Add(new EntityChange(EntityChangeKind.Added, entity));
        return this;
    }

    public ChangeSet Removed(Entity entity)
    {
        Changes.Add(new EntityChange(EntityChangeKind.Removed, entity));
        return this;
    }

    public ChangeSet Modified(Entity entity)
    {
        Changes.Add(new EntityChange(EntityChangeKind.Modified, entity));
        return this;
    }

    public void Merge(ChangeSet other)
    {
        Changes.AddRange(other.Changes);
        AffectedRegions.UnionWith(other.AffectedRegions);
    }
}
