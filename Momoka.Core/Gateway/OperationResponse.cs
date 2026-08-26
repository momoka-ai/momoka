using System.Text.Json.Nodes;

namespace Momoka.Core;

/// <summary>操作响应信封（服务器 → 客户端）。</summary>
public sealed record OperationResponse(bool Success, JsonNode? Payload, string? Error);
