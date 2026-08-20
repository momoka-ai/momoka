using Momoka.Home.Entities;
using Momoka.Home.Layouts;
using Momoka.Home.Primitives;
using Momoka.Home.Entities.Components;
using Momoka.Home.Entities.Properties;
namespace Momoka.Home;

/// <summary>
/// A unit of living space: the fully-3D, multi-layer spatial root of a
/// residence — the final form of space. Everything — floor slabs, ceilings,
/// walls, furniture, yard objects — is an <see cref="Entity"/> placed in the
/// single root grid, so placement and collision run directly against one root
/// space with root-absolute coordinates (no nested offset chains). UnitLayout
/// owns the entity list and the placement operations; <see cref="Voxels"/> is
/// the pure cell grid underneath. Space semantics — rooms / walkable areas —
/// are the <see cref="Regions"/> layer (replacing the retired floor-plan
/// graphs).
/// </summary>
public sealed class UnitLayout : IEntitySource, IVoxelSource<Entity>, IEntityRelationSource
{
    public VoxelLayout<Entity> Voxels { get; set; }
    public VoxelLayout<Region> Regions { get; set; }
    public List<Entity> Entities { get; init; }

    /// <summary>宿主表面反向索引（子 → 父表面）。与 <see cref="PlacementLayoutSource.Entities"/>
    /// 同步维护（同生共死，均只在 Add / Remove 内修改）；森林不变量保证每实体至多一项。
    /// 运行时登记态——不序列化（存档加载后与表面 Items 一并重建，待实现）。</summary>
    private readonly Dictionary<Entity, PlacementLayoutSource> _hostOf = new();

    public UnitLayout()
    {
        Voxels = new();
        Regions = new();
        Entities = new();
    }

    public UnitLayout(VoxelLayout<Entity> voxelLayout, VoxelLayout<Region> regionLayout, List<Entity> entities)
    {
        Voxels = voxelLayout;
        Regions = regionLayout;
        Entities = entities;
    }

    public record class AtQuery(UnitLayout Source, Int3 Pos)
    {
        public Entity? Entity
        {
            get => Source.Voxels[Pos];
            set => Source.Voxels[Pos] = value;
        }

        public Region? Region
        {
            get => Source.Regions[Pos];
            set
            {
                var voxels = Source.Voxels;
                var regions = Source.Regions;
                if (voxels[Pos].IsImmutable())
                {
                    return;
                }

                var column = voxels.GetIteratorAt(Pos.X, Pos.Z);
                int? ceiling = column
                    .Where(c => c.Y > Pos.Y && c.Value.IsImmutable())
                    .Select(c => (int?)c.Y)
                    .FirstOrDefault();

                // 只考虑被上下结构范围夹住的列
                if (ceiling is null ||
                    !column.Any(c => c.Y < Pos.Y && c.Value.IsImmutable()))
                {
                    return;
                }

                for (var y = ceiling.Value - 1; y >= column.MinY && !voxels[new Int3(Pos.X, y, Pos.Z)].IsImmutable(); y--)
                {
                    regions[new Int3(Pos.X, y, Pos.Z)] = value;
                }
            }
        }
    }

    /// <summary>按世界位置查询体素格内容（格长随 <see cref="Voxels"/>，与放置 / 删除一致）。</summary>
    public AtQuery At(Position pos) => new(this, Voxels.GetAsRelative(pos.Absolute()));

    // ── Entity placement / removal / queries ────────────

    /// <summary>
    /// True if placing <paramref name="src"/> at <paramref name="position"/>
    /// (world units, cm) intersects the specific <paramref name="dest"/> entity
    /// (dest voxels vs src voxels) — 实体对体积判定，委托 <see cref="Momoka.Home.Geometry.Volume.Intersects(Momoka.Home.Primitives.Int3, Momoka.Home.Geometry.Volume, Momoka.Home.Primitives.Int3)"/>。
    /// </summary>
    public bool IsCollided(Entity dest, Entity src, Float3 position)
    {
        var anchor = Voxels.GetAsRelative(position);
        var destAnchor = Voxels.GetAsRelative(dest.Transform.Position);
        return src.Volume.Intersects(anchor, dest.Volume, destAnchor);
    }

    /// <summary>
    /// 将物件加入体素空间，自动寻找可放置位置：网格 Bound 内逐 XZ 列自底向上
    /// 扫描，落点为"体积不碰撞且下方有支撑"（地面 = Bound 底格，或下方格为
    /// immutable 固定结构）的最低位置；找不到（未设 Bound / 无处可放）返回 false。
    /// 放置成功后与 <see cref="Add(Entity, Position)"/> 等价——**无附着语义**，
    /// 自动寻位不推断宿主（需要附着时由编辑器经视线探测后显式
    /// <see cref="Add(Entity, Position, PlacementLayoutSource)"/>）。
    /// </summary>
    /// <remarks>自动寻位为低频操作（编辑器拖放物件），全量扫描成本可接受。</remarks>
    public bool Add(Entity entity)
    {
        if (!Voxels.Bound.Valid)
            return false;
        var min = Voxels.GetAsRelative(Voxels.Bound.Min);
        var max = Voxels.GetAsRelative(Voxels.Bound.Max);
        for (var z = min.Z; z <= max.Z; z++)
            for (var x = min.X; x <= max.X; x++)
                for (var y = min.Y; y <= max.Y; y++)
                {
                    var anchor = new Int3(x, y, z);
                    if (!(y == min.Y || Voxels[anchor.Offset(0, -1, 0)].IsImmutable()))
                        continue; // 下方无支撑（悬空）
                    if (this.IsCollidedVolume(new Position(anchor, Voxels.Length), entity.Volume) is not null)
                        continue; // 与现有实体碰撞
                    return Add(entity, new Position(anchor, Voxels.Length));
                }
        return false;
    }

    /// <summary>
    /// 将物件按指定位置加入体素根空间（世界 cm）。**无附着语义**——宿主（表面）
    /// 关系不在此推断：根物件（地面 / 墙 / 天花板等）用本方法；需要附着关系的
    /// 物件用带 host 的重载（编辑器经视角检测确定宿主后显式传入）。
    /// 与现有实体碰撞时返回 false。
    /// </summary>
    public bool Add(Entity entity, Position position)
    {
        if (this.IsCollidedVolume(position, entity.Volume) is not null)
            return false;

        entity.Transform = entity.Transform with { Position = position.Absolute() };
        var anchor = position.Rescale(Voxels.Length).AsInt3();
        foreach (var cell in entity.Volume.Cells3D())
            Voxels[anchor + cell] = entity;
        Entities.Add(entity);
        return true;
    }

    /// <summary>
    /// 将物件附着到放置表面上：校验物件的期望类别（<see cref="Property.RotationAlignment"/>，
    /// 缺省 <see cref="RotationAlignment.Upside"/>）后放置并登记表面宿主
    /// （级联回落 / 被依赖检查用）。**底面恒贴合建模约定**：物件局部 -Y 恒为接触面
    /// （建模规范，无需声明），贴合姿态由表面方向推导（渲染端处理），
    /// 物件的 <see cref="Entity.Transform"/> 旋转表达视觉 / 朝向修正。
    /// 斜表面（如坡屋顶）可正常附着——体素占位仍轴对齐，与表面姿态无关。
    /// </summary>
    /// <remarks>
    /// - <paramref name="source"/> 由调用方（编辑器）提取——经视线探测
    ///   （<c>FindItemsOnLine</c> / <c>FindItemsInCone</c>）直接命中目标表面，
    ///   其宿主必已放置，无需在此校验。
    /// - 拒绝"宿主即自身"：<paramref name="entity"/> 已放置（其表面组件可能被再次
    ///   选中为目标）时返回 false——避免表面自引用导致级联删除异常。
    /// - 期望类别匹配见 <see cref="RotationAlignmentExtensions.Matches"/>：
    ///   精确匹配，Horizontal 额外接受 Upside / Downside。
    /// - 编辑器流程：视线探测找到表面 → 本方法显式附着（position 落点由编辑器保证）。
    /// </remarks>
    public bool Add(Entity entity, Position position, PlacementLayoutSource source)
    {
        if (Entities.Contains(entity))
            return false; // 宿主即自身（或重复放置）：已放置物件不可再作目标
        if (!entity.GetValue<RotationAlignment>(Property.RotationAlignment)
                .Matches(source.Transform.RotationAlignment))
            return false; // 期望类别不匹配

        if (!Add(entity, position)) // 碰撞检查 + 放置 + 登记（无附着语义共用）
            return false;
        source.Entities.Add(entity); // 登记表面宿主
        _hostOf[entity] = source; // 反向索引（子 → 父表面）
        return true;
    }

    /// <summary>物件的宿主表面（其附着所在表面）；根物件（无宿主）返回 null。</summary>
    public PlacementLayoutSource? FindHostEntity(Entity entity) =>
        _hostOf.GetValueOrDefault(entity);

    /// <summary>
    /// 将物件从体素空间删除（回落"未放置"池，物件本体保留在 Residence 总目录）。
    /// **连带回落其表面上的所有物件**（A 上的 B、B 上的 C 一并）——实体不能悬空，
    /// 移除宿主即回落其上的物件。物件不存在时返回 false。
    /// 回落 = 清除体素投影 + 移出已放置列表；宿主关系（表面 Entities）同步清理。
    /// </summary>
    /// <remarks>
    /// - 语义单一：无"拒绝删除"保护模式——删除前的确认（表面是否有物件）是
    ///   编辑器会话的 UI 决策（检查 <c>PlacementLayoutSource.Entities</c>），库层不拒绝。
    /// - 无环不变量：Items 图为森林——<see cref="Add(Entity, Position, PlacementLayoutSource)"/>
    ///   拒绝已放置实体（<see cref="Entities"/> 全局判断，与层数无关），保证每实体
    ///   至多一个宿主，故级联递归深度 = 放置链深，不会无限递归。
    /// </remarks>
    public bool Remove(Entity entity)
    {
        if (!Entities.Contains(entity))
            return false;

        // 级联：表面上的物件递归回落（先快照，避免遍历中修改）
        foreach (var item in entity.GetComponents<PlacementLayoutSource>().SelectMany(s => s.Entities).ToList())
            Remove(item);
        foreach (var s in entity.GetComponents<PlacementLayoutSource>())
            s.Entities.Clear();

        // 反登记：从宿主表面移除自己（O(1)——森林不变量保证每实体至多一个宿主，
        // 反向索引直达，无需全量扫描）
        if (_hostOf.TryGetValue(entity, out var host))
        {
            host.Entities.Remove(entity);
            _hostOf.Remove(entity);
        }

        Entities.Remove(entity);
        var cs = Voxels.GetAsRelative(entity.Transform.Position);
        foreach (var cell in entity.Volume.Cells3D())
        {
            var pos = cs + cell;
            if (Voxels[pos] == entity)
                Voxels[pos] = default;
        }
        return true;
    }

    /// <summary>将指定位置（世界 cm）的物件删除（含其表面物件的连带回落），返回被删除的物件；无物件时返回 null。</summary>
    public Entity? Remove(Position position)
    {
        var entity = At(position).Entity;
        return entity is not null && Remove(entity) ? entity : null;
    }

    /// <summary>按唯一 Id 查找已放置的物件。</summary>
    public Entity? Find(Guid id) =>
        Entities.FirstOrDefault(e => e.Id == id);

    /// <summary>
    /// 装载恢复（服务端装载路径用）：体素网格是"已放置"的持久化真相，
    /// 实体列表与宿主登记是运行期登记态（未序列化）——从网格重建：
    /// ① 收集网格中出现的实体去重为已放置列表（按 Id 排序，确定性）；② 清空既有登记态；
    /// ③ 按空间覆盖推断宿主（实体锚点被某已放置实体的表面格覆盖 → 挂宿主）。
    /// </summary>
    public void RestorePlacementFromGrid()
    {
        var placed = new HashSet<Entity>();
        foreach (var chunk in Voxels.Chunks)
            foreach (var cell in chunk.Cells())
                placed.Add(cell.Value);

        foreach (var entity in Entities)
            foreach (var s in entity.GetComponents<PlacementLayoutSource>())
                s.Entities.Clear();
        _hostOf.Clear();
        Entities.Clear();

        Entities.AddRange(placed.OrderBy(e => e.Id));

        foreach (var entity in Entities)
        {
            var anchor = Voxels.GetAsRelative(entity.Transform.Position);
            foreach (var other in Entities)
            {
                if (other == entity)
                    continue;
                foreach (var source in other.GetComponents<PlacementLayoutSource>())
                {
                    if (source.Layout is null || source.Layout.Size.X <= 0)
                        continue;
                    if (SurfaceCovers(source, anchor))
                    {
                        source.Entities.Add(entity);
                        _hostOf[entity] = source;
                        goto next;
                    }
                }
            }
            next:;
        }
    }

    /// <summary>表面格网中是否有格映射到 <paramref name="anchor"/>（世界格）。</summary>
    private static bool SurfaceCovers(PlacementLayoutSource source, Int3 anchor)
    {
        var size = source.Layout.Size;
        for (var z = 0; z < size.Z; z++)
            for (var x = 0; x < size.X; x++)
            {
                var rel = new Int2(x, z);
                if (source.Layout[rel] && source.AsAbsolute(rel) == anchor)
                    return true;
            }
        return false;
    }

    /// <summary>
    /// 目标实体及其表面上物件的传递闭包（级联回落集，含目标）——移除目标前的
    /// 完整影响面，供命令级撤销快照与变更通知使用。顺序 = <see cref="Remove(Entity)"/>
    /// 的递归移除顺序（子先于父、目标最后）。
    /// </summary>
    public List<Entity> CascadeOf(Entity entity)
    {
        var result = new List<Entity>();
        void Collect(Entity e)
        {
            foreach (var item in e.GetComponents<PlacementLayoutSource>().SelectMany(s => s.Entities).ToList())
            {
                Collect(item);
                result.Add(item);
            }
        }
        Collect(entity);
        result.Add(entity);
        return result;
    }

    /// <summary>
    /// 将已放置物件移动到新位置（世界 cm）：其表面上的物件随宿主同位移
    /// （相对附着保持，实体不能悬空的不变量不破坏），宿主登记同步迁移
    /// （<paramref name="host"/> = 新宿主表面，null = 移回根）。
    /// 碰撞检查排除自身与随移的子物件；任一新格碰撞 → 整体回滚并返回 false（无残留）。
    /// </summary>
    /// <remarks>
    /// - 位置按 <see cref="VoxelLayout{T}.GetAsRelative"/> 取格；新格超出世界幅界
    ///   （<see cref="Bound.MaxValue"/>，setter 静默丢弃的边界）时拒绝。
    /// - 不破坏 <see cref="Entities"/> 列表顺序（Region 全量构建的 Id 确定性依赖
    ///   实体列表顺序稳定）。
    /// </remarks>
    public bool Move(Entity entity, Position position, PlacementLayoutSource? host = null)
    {
        if (!Entities.Contains(entity))
            return false;
        if (host is not null && entity.GetComponents<PlacementLayoutSource>().Contains(host))
            return false; // 宿主即自身

        var group = CascadeOf(entity);
        var groupSet = group.ToHashSet();
        var delta = position.Rescale(Voxels.Length).AsInt3() - Voxels.GetAsRelative(entity.Transform.Position);
        if (delta == Int3.Zero && host is null)
            return true;

        // 1. 各组成员旧/新锚点
        var moves = new List<(Entity Entity, Int3 OldAnchor, Int3 NewAnchor)>();
        foreach (var e in group)
        {
            var oldAnchor = Voxels.GetAsRelative(e.Transform.Position);
            moves.Add((e, oldAnchor, oldAnchor + delta));
        }

        // 2. 暂清旧格（记录被清格 → 实体，供回滚）
        var cleared = new List<(Entity Entity, Int3 Pos)>();
        foreach (var (e, oldAnchor, _) in moves)
        {
            foreach (var cell in e.Volume.Cells3D())
            {
                var pos = oldAnchor + cell;
                if (Voxels[pos] == e)
                {
                    Voxels[pos] = default;
                    cleared.Add((e, pos));
                }
            }
        }

        // 3. 新格碰撞检查（自身已清）；超出世界幅界拒绝
        bool Collides(Int3 pos) => Voxels[pos] is { } v && !groupSet.Contains(v);
        foreach (var (e, _, newAnchor) in moves)
        {
            foreach (var cell in e.Volume.Cells3D())
            {
                var pos = newAnchor + cell;
                if (!Bound.IsValid(pos.ToFloat3()) || Collides(pos))
                {
                    foreach (var (re, rpos) in cleared)
                        Voxels[rpos] = re;
                    return false;
                }
            }
        }

        // 4. 应用：写新格 + 更新 Transform + 迁移宿主登记（仅目标）
        foreach (var (e, oldAnchor, newAnchor) in moves)
        {
            e.Transform = e.Transform with { Position = Voxels.GetAsAbsolute(newAnchor) };
            foreach (var cell in e.Volume.Cells3D())
                Voxels[newAnchor + cell] = e;
        }

        if (host is not null)
        {
            if (_hostOf.TryGetValue(entity, out var oldHost))
            {
                oldHost.Entities.Remove(entity);
                _hostOf.Remove(entity);
            }
            host.Entities.Add(entity);
            _hostOf[entity] = host;
        }
        else if (_hostOf.TryGetValue(entity, out var oldHost))
        {
            oldHost.Entities.Remove(entity);
            _hostOf.Remove(entity);
        }
        return true;
    }

    /// <summary>
    /// Clears the grid and re-rasterizes every held entity — a forced flush
    /// after direct low-level cell writes.
    /// </summary>
    /// <remarks>
    /// 已弃用：全量重栅格化 O(n) 全扫，且掩盖"低层直接写格"本身的问题。
    /// 待重写：低层写格应改为经过 UnitLayout 的受控通道（如事件 / 脏格跟踪），
    /// 而不是事后强制重建。重写后移除本方法。
    /// </remarks>
    [Obsolete("待重写：应经受控通道写入而非强制全量重栅格化")]
    public void Rebuild()
    {
        Voxels.Clear();
        foreach (var entity in Entities)
        {
            var cs = Voxels.GetAsRelative(entity.Transform.Position);
            foreach (var cell in entity.Volume.Cells3D())
            {
                Voxels[cs + cell] = entity;
            }
        }
    }
}
