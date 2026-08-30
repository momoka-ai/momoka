using System.Text.Json.Nodes;

namespace Momoka.Core;

/// <summary>
/// 网关统一响应信封（服务器 → 客户端）：<see cref="Success"/> 为是否成功；<see cref="Payload"/>
/// 为操作结果（行为回执恒 null，事实经广播下发）；<see cref="Error"/> 为失败原因（fail-soft）。
/// </summary>
public sealed record GatewayResponse(bool Success, JsonNode? Payload, string? Error);
