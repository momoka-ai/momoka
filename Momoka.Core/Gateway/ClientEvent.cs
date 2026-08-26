using System.Text.Json.Nodes;

namespace Momoka.Core;

/// <summary>线上事件信封（服务器 → 客户端广播 / 客户端 → 服务器上报）。</summary>
public sealed record ClientEvent(string EventId, JsonNode? Payload);
