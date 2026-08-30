using System.Text.Json.Nodes;

namespace Momoka.Core;

/// <summary>
/// 网关统一请求信封（客户端 → 服务器）：<see cref="Id"/> 为路由键（操作 = operationId，
/// 行为 = 事实类型 FullName），<see cref="Payload"/> 为类型擦除载荷（操作请求 / 行为 Intent）。
/// InvokeOperation 与 Post 共用同一信封（类型擦除传输的必需 DTO，对调用方隐藏）。
/// </summary>
public sealed record GatewayRequest(string Id, JsonNode? Payload);
