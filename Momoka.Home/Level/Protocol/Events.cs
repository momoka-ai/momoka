using Momoka.Home.Entities;
using Momoka.Home.Geometry;
using Momoka.Home.Primitives;
using Momoka.Home.Entities.Components;
using Momoka.Home.Entities.Properties;
namespace Momoka.Home.Level.Protocol;

/// <summary>事件帧标记（服务器 → 客户端，Pub/Sub 广播）。</summary>
public interface IEventFrame { }

[FrameType("entity_created")]
public sealed class EntityCreatedEvent : IEventFrame
{
    public Entity Entity { get; set; } = null!;
}

/// <summary>布局变更帧（实体级，替代 ChangeSet 上线——无 Old / 无脏块）。</summary>
[FrameType("layout_changed")]
public sealed class LayoutChangedEvent : IEventFrame
{
    public uint Version { get; set; }
    public EntityDelta[] EntityDelta { get; set; } = Array.Empty<EntityDelta>();
}

[FrameType("save_completed")]
public sealed class SaveCompletedEvent : IEventFrame { }

[FrameType("snapshot")]
public sealed class SnapshotEvent : IEventFrame
{
    /// <summary>住宅类型（<see cref="UnitType"/> 名称；元数据归云端账号，本地仅类型与 Home 实体属性）。</summary>
    public string Type { get; set; } = "";
    public Entity[] Entities { get; set; } = Array.Empty<Entity>();
    public Guid[] PlacedEntityIds { get; set; } = Array.Empty<Guid>();
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

/// <summary>模板目录条目（Key 显式带上——EntityTemplate.Key 是 [JsonIgnore] 的派生键）。</summary>
public sealed class TemplateCatalogEntry
{
    public string Key { get; set; } = "";
    public Volume? Volume { get; set; }
    public List<Property> Properties { get; set; } = new();
    public List<string> Components { get; set; } = new();
}
