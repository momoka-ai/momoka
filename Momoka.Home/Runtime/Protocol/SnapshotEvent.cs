using Momoka.Home.Levels.Entities;
using Momoka.Home.Levels.Volumes;
using Momoka.Home.Levels.Entities.Properties;
using Momoka.Home.Data.Json.Converters;
using Newtonsoft.Json;
namespace Momoka.Home.Runtime.Protocol;

/// <summary>
/// 全量状态同步载荷（<c>get_snapshot</c> 结果 / 客户端重同步）。
/// 传输事件包装已由 Core 网关的 <c>IHomeClient</c> 强类型方法取代——
/// 本类型仅作查询返回 DTO。
/// </summary>
public sealed class SnapshotEvent
{
    /// <summary>住宅类型（<see cref="Momoka.Home.Levels.LevelType"/> 名称；元数据归云端账号，本地仅类型与 Home 实体属性）。</summary>
    public string Type { get; set; } = "";
    public Entity[] Entities { get; set; } = Array.Empty<Entity>();
    public Guid[] PlacedEntityIds { get; set; } = Array.Empty<Guid>();
    public TemplateCatalogEntry[] TemplateCatalog { get; set; } = Array.Empty<TemplateCatalogEntry>();
    public string TemplateVersion { get; set; } = "";
    public uint Version { get; set; }
}

/// <summary>模板目录条目（Key 显式带上——EntityTemplate.Key 是 [JsonIgnore] 的派生键）。</summary>
public sealed class TemplateCatalogEntry
{
    public string Key { get; set; } = "";
    [JsonConverter(typeof(JsonGeometryConverter))]
    public Volume? Volume { get; set; }
    public List<Property> Properties { get; set; } = new();
    public List<string> Components { get; set; } = new();
}
