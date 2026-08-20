using Momoka.Home.Layouts;
using Momoka.Home.Primitives;
using Momoka.Home.Entities;
namespace Momoka.Home.Editing;

/// <summary>
/// Region 增量维护（§4）：结构变更（<see cref="Property.IsImmutable"/> 实体的
/// 增删改）后触发——先全量重建（正确性基建），再对新的连通分量做 **Id 稳定映射**：
/// 与新 Region 占用格重叠最大的旧 Region 保留其 Id 与 Name（未受影响 Region 的
/// Id 与数据不变——Ui 增量更新 / 高亮 / 命名的前提）；真正的新分量取后续空闲 Id。
/// 输出受影响 Region Id 集进 ChangeSet；非结构变更直接跳过（Region 层与家具无关）。
/// </summary>
public sealed class RegionMaintainer
{
    /// <summary>当前 Region 层（无结构实体时空表）。</summary>
    public ColumnLayout<Region>? Regions { get; private set; }

    private ColumnLayout<Region>? _previous;
    private int _nextId = 1;

    /// <summary>全量重建（首次打开 / 加载后调用）。</summary>
    public void Rebuild(UnitLayout unit)
    {
        var fresh = Region.BuildLayout(unit);
        _nextId = NextIdAfter(fresh);
        Regions = fresh;
        _previous = fresh;
    }

    /// <summary>
    /// 注入装载层（<see cref="ServerLevelData.Load"/>）：保留持久化的 Region Id 与
    /// 用户命名（<c>RegionsCodec</c> 读出的列布局），作为后续增量维护的基线。
    /// 不走全量重建——重建会重赋 Id、丢失命名。
    /// </summary>
    public void Adopt(ColumnLayout<Region> regions)
    {
        Regions = regions;
        _previous = regions;
        _nextId = regions.Cells().Select(c => c.Value.Id).DefaultIfEmpty(0).Max() + 1;
    }

    /// <summary>清空维护状态（重新装载前调用）。</summary>
    public void Reset()
    {
        Regions = null;
        _previous = null;
        _nextId = 1;
    }

    /// <summary>
    /// 结构变更提交后触发：重建 + Id 稳定映射 + 受影响 Region 集计算。
    /// 返回是否发生重建（变更是否为结构变更）。
    /// </summary>
    public bool ApplyChange(UnitLayout unit, ChangeSet changes)
    {
        if (!IsStructural(changes))
            return false;

        var fresh = Region.BuildLayout(unit);
        Regions = Stabilize(fresh);
        changes.AffectedRegions.UnionWith(ComputeAffected(_previous, Regions));
        _previous = Regions;
        return true;
    }

    private static bool IsStructural(ChangeSet changes) =>
        changes.Changes.Any(c => c.Entity.IsImmutable());

    /// <summary>
    /// Id 稳定映射：新 Region 与旧 Region 的占用格重叠计数，贪心保留最大重叠者的
    /// Id 与 Name；无重叠来源的新分量取 <see cref="_nextId"/> 递增。
    /// </summary>
    private ColumnLayout<Region> Stabilize(ColumnLayout<Region> fresh)
    {
        if (_previous is null)
        {
            _nextId = NextIdAfter(fresh);
            return fresh;
        }

        var oldById = new Dictionary<int, Region>();
        foreach (var (_, r) in _previous.Cells())
            oldById.TryAdd(r.Id, r);

        // 逐格重叠计数：新 Region × 旧 Region 共享格数
        var overlap = new Dictionary<(Region NewR, Region OldR), int>();
        foreach (var (pos, newRegion) in fresh.Cells())
        {
            if (_previous.At(pos.X, pos.Y, pos.Z) is not { } oldRegion)
                continue;
            var key = (newRegion, oldRegion);
            overlap.TryGetValue(key, out var n);
            overlap[key] = n + 1;
        }

        // 贪心分配 Id：每个新 Region 取与之重叠最大且未被认领的旧 Region
        var claimed = new HashSet<Region>();
        var idByNew = new Dictionary<Region, int>();
        var nameByNew = new Dictionary<Region, string>();
        foreach (var newRegion in fresh.Cells().Select(c => c.Value).Distinct())
        {
            var best = overlap
                .Where(kv => ReferenceEquals(kv.Key.NewR, newRegion) && !claimed.Contains(kv.Key.OldR))
                .OrderByDescending(kv => kv.Value)
                .Select(kv => kv.Key.OldR)
                .FirstOrDefault();
            if (best is not null)
            {
                claimed.Add(best);
                idByNew[newRegion] = best.Id;
                nameByNew[newRegion] = best.Name;
            }
            else
            {
                idByNew[newRegion] = _nextId++;
                nameByNew[newRegion] = newRegion.Name;
            }
        }

        // 逐格写回（共享 Region 引用——Region.Name 可改、RegionsCodec 依赖引用语义）
        var remapped = new ColumnLayout<Region>(_ => false) { Bound = fresh.Bound };
        var regionByNew = new Dictionary<Region, Region>();
        foreach (var (pos, r) in fresh.Cells())
        {
            if (!regionByNew.TryGetValue(r, out var remap))
            {
                remap = new Region(idByNew[r], r.Bounds, r.Volume, r.Area) { Name = nameByNew[r] };
                regionByNew[r] = remap;
            }
            remapped.SetCell(pos.X, pos.Y, pos.Z, remap);
        }

        _nextId = idByNew.Values.Count > 0 ? idByNew.Values.Max() + 1 : 1;
        return remapped;
    }

    /// <summary>
    /// 新旧 Region 层逐格比对：格上 Region 的 Id 或几何（Bounds / Volume / Area）
    /// 不一致 → 新旧两侧 Id 均入受影响集。Region 实例每次重建都新建，故按
    /// "Id + 几何" 判等——未受影响的 Region 即便重建也不入受影响集。
    /// </summary>
    private static HashSet<int> ComputeAffected(ColumnLayout<Region>? before, ColumnLayout<Region>? after)
    {
        var affected = new HashSet<int>();
        if (after is null)
            return affected;
        if (before is null)
        {
            foreach (var (_, r) in after.Cells())
                affected.Add(r.Id);
            return affected;
        }

        foreach (var (pos, r) in before.Cells())
        {
            var now = after.At(pos.X, pos.Y, pos.Z);
            if (!SameRegion(now, r))
            {
                affected.Add(r.Id);
                if (now is not null)
                    affected.Add(now.Id);
            }
        }
        foreach (var (pos, r) in after.Cells())
        {
            var old = before.At(pos.X, pos.Y, pos.Z);
            if (!SameRegion(old, r))
            {
                affected.Add(r.Id);
                if (old is not null)
                    affected.Add(old.Id);
            }
        }
        return affected;
    }

    private static bool SameRegion(Region? a, Region? b) =>
        a is not null && b is not null
        && a.Id == b.Id
        && a.Volume == b.Volume
        && a.Area == b.Area
        && a.Bounds == b.Bounds;

    private static int NextIdAfter(ColumnLayout<Region> regions)
    {
        var cells = regions.Cells().Select(c => c.Value).ToList();
        return cells.Count > 0 ? cells.Max(r => r.Id) + 1 : 1;
    }
}
