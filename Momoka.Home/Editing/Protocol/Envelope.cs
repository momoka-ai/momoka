using Newtonsoft.Json.Linq;
namespace Momoka.Home.Editing.Protocol;

/// <summary>
/// 连接级信封：<see cref="Type"/> 为请求 / 事件名（<see cref="FrameRegistry"/> 判别），
/// <see cref="Payload"/> 为 JSON 载荷。<see cref="PayloadFormat"/> 预留二进制通道
/// （Phase 1 恒 <c>json</c>）。
/// </summary>
public sealed class Envelope
{
    public uint ProtocolVersion { get; set; } = Frames.Version;
    public uint Seq { get; set; }
    public string RequestId { get; set; } = "";
    public string Type { get; set; } = "";
    public string PayloadFormat { get; set; } = Frames.JsonPayload;
    public JToken? Payload { get; set; }
}
