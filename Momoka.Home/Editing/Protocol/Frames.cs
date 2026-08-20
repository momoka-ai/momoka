using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
namespace Momoka.Home.Editing.Protocol;

/// <summary>
/// 帧组装助手与协议常量（JSON 帧）。Phase 1 载荷恒 <c>json</c>；
/// <see cref="Envelope.PayloadFormat"/> = <c>msgpack</c> 为 Region 二进制通道预留
/// （MessagePack 包引用保留，当前协议不使用）。
/// </summary>
public static class Frames
{
    public const uint Version = 1;
    public const string JsonPayload = "json";
    public const string MsgPackPayload = "msgpack";

    public static Envelope RequestFrame(string type, uint seq, string requestId, IRequestFrame request) => new()
    {
        Seq = seq,
        RequestId = requestId,
        Type = type,
        PayloadFormat = JsonPayload,
        Payload = JToken.FromObject(request, JsonSerializer.Create(Settings.JsonSerialization)),
    };

    public static Envelope EventFrame(string type, uint seq, string? requestId, IEventFrame frame) => new()
    {
        Seq = seq,
        RequestId = requestId ?? "",
        Type = type,
        PayloadFormat = JsonPayload,
        Payload = JToken.FromObject(frame, JsonSerializer.Create(Settings.JsonSerialization)),
    };
}

/// <summary>Pub/Sub topic（宿主按连接 fan-out；Home 内订阅即本常量）。</summary>
public static class Topics
{
    public const string Layout = "layout";
    public const string Entities = "entities";
    public const string Regions = "regions";
    public const string Lifecycle = "lifecycle";

    public static string Of(IEventFrame frame) => frame switch
    {
        LayoutChangedEvent => Layout,
        EntityCreatedEvent => Entities,
        RegionChangedEvent => Regions,
        SaveCompletedEvent or ErrorEvent => Lifecycle,
        _ => Lifecycle,
    };
}
