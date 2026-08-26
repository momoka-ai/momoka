using System.Text.Json.Nodes;

namespace Momoka.Core;

/// <summary>
/// 网关强类型客户端契约（服务器 → 客户端单向方法）。
/// </summary>
public interface IGatewayClient
{
    /// <summary>服务器 → 客户端事件广播。</summary>
    Task ClientEvent(string eventId, JsonNode? payload);
}
