namespace Momoka.Core;

/// <summary>操作上下文（操作处理器的调用者信息）。授权归 Security 期。</summary>
public sealed record OperationContext(string OperationId, Client Caller);
