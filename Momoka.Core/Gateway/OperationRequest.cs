using System.Text.Json.Nodes;

namespace Momoka.Core;

/// <summary>操作请求信封（客户端 → 服务器）。</summary>
public sealed record OperationRequest(string OperationId, JsonNode? Payload);
