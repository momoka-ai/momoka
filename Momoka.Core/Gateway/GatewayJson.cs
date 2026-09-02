using System.Text.Json;

namespace Momoka.Core;

/// <summary>
/// 全局单一序列化选项（STJ 一统）：snake_case 命名策略。
/// SignalR JSON 协议与 Packet / Post 层信封、载荷共用同一 options。
/// </summary>
public static class GatewayJson
{
    /// <summary>共享序列化选项（snake_case）。</summary>
    public static JsonSerializerOptions Options { get; } = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };
}
