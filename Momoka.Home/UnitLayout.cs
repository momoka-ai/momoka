using Momoka.Home.Components;
using Momoka.Home.Entities;
using Momoka.Home.Layouts;
using Momoka.Home.Primitives;
using Momoka.Home.Properties;
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
public sealed class UnitLayout : IEntitySource, IVoxelSource<Entity>
{
    public VoxelLayout<Entity> Voxels { get; set; }
    public VoxelLayout<Region> Regions { get; set; }
    public List<Entity> Entities { get; init; }

    public float VoxelSize { get; set; } = 10.0f;

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

    /// <summary>
    /// All placement surfaces of the space, with their pose: each entity's
    /// placement layouts (via its <see cref="PlacementLayoutSource"/> components —
    /// a floor slab's top face, a shelf board…). Carries <see cref="Transform"/>
    /// (position + facing), unlike the bare layout grid.
    /// </summary>
    public IEnumerable<PlacementLayoutSource> Surfaces => Entities
        .SelectMany(e => e.GetComponents<PlacementLayoutSource>());

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
    /// 将物件加入体素空间，自行寻找一个可放置位置。
    /// </summary>
    /// <remarks>存根：自动寻位（扫描 Bound 内不碰撞的位置）待实现。</remarks>
    public bool Add(Entity entity) =>
        throw new NotImplementedException("自动寻位待实现：扫描 Bound 内可放置位置");

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
        var anchor = Voxels.GetAsRelative(position.Absolute());
        foreach (var cell in entity.Volume.Cells3D())
            Voxels[anchor + cell] = entity;
        Entities.Add(entity);
        return true;
    }

    /// <summary>
    /// 将物件附着到放置表面上：校验表面轴对齐与物件的期望类别
    /// （<see cref="Property.DirectionAlignment"/>，缺省 Any）后放置并登记表面宿主
    /// （级联回落 / 被依赖检查用）。**底面恒贴合建模约定**：物件局部 -Y 恒为接触面
    /// （建模规范，无需声明），贴合姿态由表面方向推导（渲染端处理），
    /// 物件的 <see cref="Entity.Transform"/> 旋转表达视觉 / 朝向修正。
    /// </summary>
    /// <remarks>
    /// - <paramref name="source"/> 由调用方（编辑器）提取——经视线探测
    ///   （<c>FindItemsOnLine</c> / <c>FindItemsInCone</c>）直接命中目标表面，
    ///   其宿主必已放置，无需在此校验。
    /// - 表面必须轴对齐（斜表面直接拒绝——体素放置不支持斜姿态）。
    /// - 拒绝"宿主即自身"：<paramref name="entity"/> 已放置（其表面组件可能被再次
    ///   选中为目标）时返回 false——避免表面自引用导致级联删除异常。
    /// - 期望类别匹配：Any 恒过；Horizontal 接受 Upside / Downside 两种水平面。
    /// - 编辑器流程：视线探测找到表面 → 本方法显式附着（position 落点由编辑器保证）。
    /// </remarks>
    public bool Add(Entity entity, Position position, PlacementLayoutSource source)
    {
        if (!source.Transform.Rotation.IsAxisAligned)
            return false;
        if (Entities.Contains(entity))
            return false; // 宿主即自身（或重复放置）：已放置物件不可再作目标

        // 期望类别校验：表面朝向类别 ∈ 期望（Any 恒过；Horizontal 接受上下水平面）
        var actual = source.Transform.Rotation.Alignment;
        var required = entity.GetValue<DirectionAlignment>(Property.DirectionAlignment);
        if (required != DirectionAlignment.Any && required != actual
            && !(required == DirectionAlignment.Horizontal && actual is DirectionAlignment.Upside or DirectionAlignment.Downside))
            return false;

        if (this.IsCollidedVolume(position, entity.Volume) is not null)
            return false;

        entity.Transform = entity.Transform with { Position = position.Absolute() };
        var anchor = Voxels.GetAsRelative(position.Absolute());
        foreach (var cell in entity.Volume.Cells3D())
            Voxels[anchor + cell] = entity;
        Entities.Add(entity);
        source.Items.Add(entity); // 登记表面宿主
        return true;
    }

    /// <summary>
    /// 将物件从体素空间删除（回落"未放置"池，物件本体保留在 Residence 总目录）。
    /// 未放置或正被依赖（其提供的表面仍有物件放置其上）时返回 false，不做删除。
    /// </summary>
    public bool Remove(Entity entity) => Remove(entity, cascade: false);

    /// <summary>
    /// 删除物件，<paramref name="cascade"/> 为 true 时级联删除其表面上的所有
    /// 物件（A 上的 B、B 上的 C 一并回落）。物件不存在时返回 false。
    /// 回落 = 清除体素投影 + 移出已放置列表；宿主关系（表面 Items）同步清理。
    /// </summary>
    /// <remarks>
    /// 无环不变量：Items 图为森林——<see cref="Add(Entity, Position, PlacementLayoutSource)"/>
    /// 拒绝已放置实体（<see cref="Entities"/> 全局判断，与层数无关），保证每实体
    /// 至多一个宿主，故级联递归深度 = 放置链深，不会无限递归。
    /// </remarks>
    public bool Remove(Entity entity, bool cascade)
    {
        if (!Entities.Contains(entity))
            return false;

        var surfaces = entity.GetComponents<PlacementLayoutSource>();
        if (!cascade && surfaces.Any(s => s.Items.Count > 0))
            return false; // 正被依赖：表面仍有物件，普通删除失败

        // 级联：表面上的物件递归回落（先快照，避免遍历中修改）
        foreach (var item in surfaces.SelectMany(s => s.Items).ToList())
            Remove(item, cascade: true);
        foreach (var s in surfaces)
            s.Items.Clear();

        // 反登记：从宿主表面移除自己
        foreach (var other in Entities)
            foreach (var s in other.GetComponents<PlacementLayoutSource>())
                s.Items.Remove(entity);

        Entities.Remove(entity);
        var cs = Voxels.GetAsRelative(entity.Transform.Position);
        foreach (var cell in entity.Volume.Cells3D())
        {
            var pos = cs + cell;
            if (Voxels[pos] == entity)
                Voxels[pos] = default!;
        }
        return true;
    }

    /// <summary>将指定位置（世界 cm）的物件删除，返回被删除的物件；无物件或删除失败（被依赖）时返回 null。</summary>
    public Entity? Remove(Position position)
    {
        var entity = At(position).Entity;
        return entity is not null && Remove(entity) ? entity : null;
    }

    /// <summary>按唯一 Id 查找已放置的物件。</summary>
    public Entity? Find(Guid id) =>
        Entities.FirstOrDefault(e => e.Id == id);

    /// <summary>
    /// Clears the grid and re-rasterizes every held entity — a forced flush
    /// after direct low-level cell writes.
    /// </summary>
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

    /// <summary>
    /// Returns all entities whose shape intersects the axis-aligned box
    /// <paramref name="min"/>–<paramref name="max"/> (inclusive). Drag-select —
    /// 占用格语义，委托 <see cref="VoxelSourceExtensions.FindItemsInBound{T}"/>。
    /// </summary>
    public IEnumerable<Entity> FindEntitiesInBound(Int2 min, Int2 max) =>
        this.FindItemsInBound(min, max);
}
