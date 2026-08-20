using Momoka.Home.Entities;
using Momoka.Home.Geometry;
using Momoka.Home.Primitives;
namespace Momoka.Home.Editing.Protocol;

/// <summary>事件帧标记（服务器 → 客户端，Pub/Sub 广播）。</summary>
public interface IEventFrame { }

[FrameType("entity_created")]
public sealed class EntityCreatedEvent : IEventFrame
{
    public Entity Entity { get; set; } = null!;
}

/// <summary>布局变更帧（实体级，替代 ChangeSet 上线——无 Old / 无脏块 / 无受影响 Region）。</summary>
[FrameType("layout_changed")]
public sealed class LayoutChangedEvent : IEventFrame
{
    public uint Version { get; set; }
    public EntityDelta[] EntityDelta { get; set; } = Array.Empty<EntityDelta>();
}

/// <summary>Region 同步帧：Phase 1 仅预留（区域分布 + 用户命名不可客户端算法重建）。</summary>
[FrameType("region_changed")]
public sealed class RegionChangedEvent : IEventFrame
{
    public uint Version { get; set; }
    public RegionPayload RegionPayload { get; set; } = new();
}

[FrameType("save_completed")]
public sealed class SaveCompletedEvent : IEventFrame { }

[FrameType("snapshot")]
public sealed class SnapshotEvent : IEventFrame
{
    public ResidenceMeta ResidenceMeta { get; set; } = new();
    public Entity[] Entities { get; set; } = Array.Empty<Entity>();
    public Guid[] PlacedEntityIds { get; set; } = Array.Empty<Guid>();
    public RegionPayload? RegionPayload { get; set; }
    public TemplateCatalogEntry[] TemplateCatalog { get; set; } = Array.Empty<TemplateCatalogEntry>();
    public string TemplateVersion { get; set; } = "";
    public uint Version { get; set; }
}

[FrameType("error")]
public sealed class ErrorEvent : IEventFrame
{
    public string RequestId { get; set; } = "";
    public string ErrorCode { get; set; } = "";
}

/// <summary>住宅元数据（快照 / 客户端镜像用；不序列化整 Residence——含体素与登记态）。</summary>
public sealed class ResidenceMeta
{
    public string Name { get; set; } = "";
    public string Address { get; set; } = "";
    public string Type { get; set; } = "";
    public Bound Bound { get; set; } = Bound.UnsetValue;
}

/// <summary>模板目录条目（Key 显式带上——EntityTemplate.Key 是 [JsonIgnore] 的派生键）。</summary>
public sealed class TemplateCatalogEntry
{
    public string Key { get; set; } = "";
    public Volume? Volume { get; set; }
    public List<Property> Properties { get; set; } = new();
    public List<string> Components { get; set; } = new();
}

/// <summary>Region 用户数据（分布 + 命名；Phase 1 仅占位，形状待定）。</summary>
public sealed class RegionPayload
{
    public List<RegionInfo> Regions { get; set; } = new();
}

public sealed class RegionInfo
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
}
