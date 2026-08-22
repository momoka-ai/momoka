using Newtonsoft.Json.Linq;
using Momoka.Home.Levels.Commands;
using Momoka.Home.Primitives;
namespace Momoka.Home.Runtime.Protocol;

/// <summary>
/// 操作请求 DTO（客户端 → 服务端，SignalR Hub 方法参数）。帧判别 / 序列化 / 分派
/// 由 SignalR 承担——本层只保留纯参数类型，不再有帧标记与注册表。
/// </summary>
public sealed class CreateEntityRequest
{
    public string TemplateKey { get; set; } = "";
    public string? TemplateVersion { get; set; }
}

public sealed class PlaceEntityRequest
{
    public Guid EntityId { get; set; }
    public Float3 Position { get; set; }
    public Guid? HostId { get; set; }
}

public sealed class RemoveEntityRequest
{
    public Guid EntityId { get; set; }
}

public sealed class MoveEntityRequest
{
    public Guid EntityId { get; set; }
    public Float3 Position { get; set; }
    public Guid? HostId { get; set; }
}

public sealed class RotateEntityRequest
{
    public Guid EntityId { get; set; }
    public float YawDelta { get; set; }
    public float PitchDelta { get; set; }
    public float RollDelta { get; set; }
}

public sealed class SetPropertyRequest
{
    public Guid EntityId { get; set; }
    public string Name { get; set; } = "";
    /// <summary>JSON 原生标量（bool / number / string / null=清除）；服务器按属性类型强转。</summary>
    public JToken? Value { get; set; }
}

public sealed class SetTextureRequest
{
    public Guid EntityId { get; set; }
    public string? TextureKey { get; set; }
}

public sealed class BuildWallRequest
{
    public WallSegment[] Segments { get; set; } = Array.Empty<WallSegment>();
}

public sealed class BuildOpeningRequest
{
    public Guid WallEntityId { get; set; }
    public Int3 OpeningOrigin { get; set; }
    public Int3 OpeningSize { get; set; }
    public string OpeningKey { get; set; } = "";
    public bool IsOpen { get; set; } = true;
}
