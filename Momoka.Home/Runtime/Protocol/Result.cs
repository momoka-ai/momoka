using System.Text.Json.Nodes;
namespace Momoka.Home.Runtime.Protocol;

/// <summary>
/// 请求结果（服务器 → 请求者）：<c>ok</c> + 变更后版本号；
/// <c>get_snapshot</c> / <c>create_entity</c> 的载荷走 <see cref="Payload"/>。
/// 变更结果不随 Result 回传——<c>layout_changed</c> 事件广播给所有客户端（含请求者）。
/// </summary>
public sealed class Result
{
    public bool Ok { get; set; }
    public string? ErrorCode { get; set; }
    public uint Version { get; set; }
    public JsonNode? Payload { get; set; }

    public static Result Success(uint version) => new() { Ok = true, Version = version };
    public static Result WithPayload(JsonNode payload) => new() { Ok = true, Payload = payload };
    public static Result Fail(string errorCode) => new() { Ok = false, ErrorCode = errorCode };
}
