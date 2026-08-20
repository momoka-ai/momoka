using Momoka.Home.Editing.Protocol;
using Momoka.Home.Entities;
using Momoka.Home.Geometry;
using Momoka.Home.Layouts;
using Momoka.Home.Primitives;
namespace Momoka.Home.Editing;

/// <summary>
/// 客户端只读镜像：由服务器快照 / <c>layout_changed</c> 增量驱动，本地重建占用网格
/// （实体列表是编辑器域唯一真相——从空图逐个放置）供射线 / 碰撞 / 框选预览。
/// **严格只读**：无任何写权威模型的入口——拖拽预览 = 只读查询，松手单次提交。
/// 实现 <see cref="IVoxelSource{T}"/> 以复用 Home 查询扩展（<c>FindItemsOnLine</c> 等）。
/// </summary>
public sealed class ClientLevelData : IVoxelSource<Entity>
{
    public ResidenceMeta ResidenceMeta { get; private set; } = new();

    /// <summary>全实体注册表（含未放置池）。</summary>
    public Dictionary<Guid, Entity> Registry { get; } = new();

    /// <summary>已放置实体（镜像的放置列表）。</summary>
    public List<Entity> Placed { get; } = new();

    /// <summary>由实体载荷本地重建的占用镜像（<see cref="IVoxelSource{T}.Voxels"/>）。</summary>
    public VoxelLayout<Entity> Grid { get; private set; } = new();

    /// <summary>Region 用户数据快照（Phase 1 仅占位）。</summary>
    public RegionPayload? Regions { get; private set; }

    /// <summary>模板目录（版本化）。</summary>
    public List<TemplateCatalogEntry> Templates { get; } = new();

    /// <summary>已知服务端版本。</summary>
    public uint Version { get; private set; }

    /// <summary>只读查询面（复用 <c>IVoxelSource&lt;Entity&gt;</c> 扩展：FindItemsOnLine / IsCollidedVolume / FindItemsInBound）。</summary>
    public VoxelLayout<Entity> Voxels => Grid;

    /// <summary>事件版本落后于"当前 + 1"（缺口 / 乱序）→ 请求 get_snapshot 重同步。</summary>
    public bool NeedsResync(uint serverVersion) => serverVersion != Version + 1;

    /// <summary>全量重同步：从空图重建（注册表 + 放置列表 + 占用网格 + 模板目录）。</summary>
    public void ApplySnapshot(SnapshotEvent snapshot)
    {
        ResidenceMeta = snapshot.ResidenceMeta;
        Registry.Clear();
        Placed.Clear();
        foreach (var entity in snapshot.Entities)
            Registry[entity.Id] = entity;
        var placedIds = snapshot.PlacedEntityIds.ToHashSet();
        foreach (var entity in snapshot.Entities)
            if (placedIds.Contains(entity.Id))
                Placed.Add(entity);
        Regions = snapshot.RegionPayload;
        Templates.Clear();
        Templates.AddRange(snapshot.TemplateCatalog);
        Version = snapshot.Version;
        RebuildGrid();
    }

    /// <summary>
    /// 增量应用（§4）：added → 注册 + 放置 + 写格；removed → 清旧格 + 移除（凭 id 取本地
    /// 注册表旧值）；modified → 清旧格 → 写新格 → 更新注册表 / 放置列表。
    /// 先按本地注册表推导脏区块（旧格 + 新格），再应用变更。返回脏区块（Ui re-mesh 提示）。
    /// </summary>
    public IReadOnlyList<Int2> Apply(EntityDelta[] deltas, uint version)
    {
        var dirty = ComputeDirtyChunks(deltas);
        foreach (var delta in deltas)
        {
            switch (delta.Kind)
            {
                case "added":
                    if (delta.Entity is not { } added)
                        break;
                    Registry[added.Id] = added;
                    Placed.Add(added);
                    WriteCells(added);
                    break;
                case "removed":
                    if (delta.EntityId is not { } removedId)
                        break;
                    if (Registry.TryGetValue(removedId, out var removed))
                    {
                        ClearCells(removed);
                        Placed.RemoveAll(e => e.Id == removedId);
                        Registry.Remove(removedId);
                    }
                    break;
                case "modified":
                    if (delta.Entity is not { } modified)
                        break;
                    if (Registry.TryGetValue(modified.Id, out var old))
                        ClearCells(old);
                    Registry[modified.Id] = modified;
                    var index = Placed.FindIndex(e => e.Id == modified.Id);
                    if (index >= 0)
                        Placed[index] = modified;
                    else
                        Placed.Add(modified);
                    WriteCells(modified);
                    break;
            }
        }
        Version = version;
        return dirty;
    }

    /// <summary>本地推导脏区块：受影响实体的旧格（本地注册表）+ 新格所在区块并集。</summary>
    public IReadOnlyList<Int2> ComputeDirtyChunks(EntityDelta[] deltas)
    {
        var dirty = new HashSet<Int2>();
        foreach (var delta in deltas)
        {
            if (delta.Entity is { } entity)
            {
                MarkCells(entity.Transform, entity.Volume, dirty);
                if (delta.EntityId is { } id && Registry.TryGetValue(id, out var old) && old != entity)
                    MarkCells(old.Transform, old.Volume, dirty);
            }
            else if (delta.Kind == "removed" && delta.EntityId is { } removedId
                     && Registry.TryGetValue(removedId, out var removedEntity))
            {
                MarkCells(removedEntity.Transform, removedEntity.Volume, dirty);
            }
        }
        return dirty.ToList();
    }

    /// <summary>实体列表 → 占用网格重建（从空图逐个放置）。</summary>
    public void RebuildGrid()
    {
        var grid = new VoxelLayout<Entity>();
        foreach (var entity in Placed)
        {
            var anchor = grid.GetAsRelative(entity.Transform.Position);
            foreach (var cell in entity.Volume.Cells3D())
                grid[anchor + cell] = entity;
        }
        Grid = grid;
    }

    private void WriteCells(Entity entity)
    {
        var anchor = Grid.GetAsRelative(entity.Transform.Position);
        foreach (var cell in entity.Volume.Cells3D())
            Grid[anchor + cell] = entity;
    }

    private void ClearCells(Entity entity)
    {
        var anchor = Grid.GetAsRelative(entity.Transform.Position);
        foreach (var cell in entity.Volume.Cells3D())
        {
            var pos = anchor + cell;
            if (Grid[pos] == entity)
                Grid[pos] = default;
        }
    }

    private void MarkCells(Transform transform, Volume? volume, HashSet<Int2> dirty)
    {
        if (volume is null)
            return;
        var anchor = new Int3(
            (int)Math.Round(transform.Position.X / Grid.Length, MidpointRounding.AwayFromZero),
            (int)Math.Round(transform.Position.Y / Grid.Length, MidpointRounding.AwayFromZero),
            (int)Math.Round(transform.Position.Z / Grid.Length, MidpointRounding.AwayFromZero));
        foreach (var cell in volume.Cells3D())
        {
            var pos = anchor + cell;
            dirty.Add(new Int2(pos.X >> 4, pos.Z >> 4));
        }
    }
}
