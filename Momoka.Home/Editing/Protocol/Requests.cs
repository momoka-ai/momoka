using Newtonsoft.Json.Linq;
using Momoka.Home.Editing.Commands;
using Momoka.Home.Primitives;
namespace Momoka.Home.Editing.Protocol;

/// <summary>请求帧标记（客户端 → 服务器，纯参数）。</summary>
public interface IRequestFrame { }

/// <summary>
/// 请求集：客户端只发请求；服务器权威处理并广播事件。
/// 载荷为 JSON 原生标量 / 模型类型（经 <see cref="Momoka.Home.Settings.JsonSerialization"/>）。
/// </summary>
[FrameType("create_entity")]
public sealed class CreateEntityRequest : IRequestFrame
{
    public string TemplateKey { get; set; } = "";
    public string? TemplateVersion { get; set; }
}

[FrameType("place_entity")]
public sealed class PlaceEntityRequest : IRequestFrame
{
    public Guid EntityId { get; set; }
    public Float3 Position { get; set; }
    public Guid? HostId { get; set; }
}

[FrameType("remove_entity")]
public sealed class RemoveEntityRequest : IRequestFrame
{
    public Guid EntityId { get; set; }
    public bool? Cascade { get; set; }
}

[FrameType("move_entity")]
public sealed class MoveEntityRequest : IRequestFrame
{
    public Guid EntityId { get; set; }
    public Float3 Position { get; set; }
    public Guid? HostId { get; set; }
}

[FrameType("rotate_entity")]
public sealed class RotateEntityRequest : IRequestFrame
{
    public Guid EntityId { get; set; }
    public float YawDelta { get; set; }
    public float PitchDelta { get; set; }
    public float RollDelta { get; set; }
}

[FrameType("set_property")]
public sealed class SetPropertyRequest : IRequestFrame
{
    public Guid EntityId { get; set; }
    public string Name { get; set; } = "";
    /// <summary>JSON 原生标量（bool / number / string / null=清除）；服务器按属性类型强转。</summary>
    public JToken? Value { get; set; }
}

[FrameType("set_texture")]
public sealed class SetTextureRequest : IRequestFrame
{
    public Guid EntityId { get; set; }
    public string? TextureKey { get; set; }
}

[FrameType("build_wall")]
public sealed class BuildWallRequest : IRequestFrame
{
    public WallSegment[] Segments { get; set; } = Array.Empty<WallSegment>();
}

[FrameType("build_opening")]
public sealed class BuildOpeningRequest : IRequestFrame
{
    public Guid WallEntityId { get; set; }
    public Int3 OpeningOrigin { get; set; }
    public Int3 OpeningSize { get; set; }
    public string OpeningKey { get; set; } = "";
    public bool IsOpen { get; set; } = true;
}

[FrameType("undo")]
public sealed class UndoRequest : IRequestFrame { }

[FrameType("redo")]
public sealed class RedoRequest : IRequestFrame { }

[FrameType("begin_edit")]
public sealed class BeginEditRequest : IRequestFrame { }

[FrameType("end_edit")]
public sealed class EndEditRequest : IRequestFrame { }

[FrameType("save")]
public sealed class SaveRequest : IRequestFrame { }

[FrameType("get_snapshot")]
public sealed class GetSnapshotRequest : IRequestFrame
{
    public uint? Version { get; set; }
}
