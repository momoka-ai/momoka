namespace Momoka.Core.Behaviors;

/// <summary>
/// 意图来源（CommandSender 风格抽象）：任何能发起行为的主体，由管线注入 <c>Execute</c>。
/// 新增来源模态（线上客户端、语音管线、自动化、Agent）只需新实现本接口，行为签名不变。
/// 来源身份用于审计与权限判定；<see cref="SendMessageAsync"/> 提供来源直通反馈
/// （客户端 → 其连接；本地模态 → 日志）。
/// </summary>
public interface IntentSource
{
    /// <summary>来源标识（审计/日志）：客户端为 <c>clientId</c>，本地模态为 <c>origin</c>。</summary>
    string Name { get; }

    /// <summary>是否远端来源（线上客户端）；本地模态为 false。</summary>
    bool IsRemote { get; }

    /// <summary>向来源直通发送消息（best-effort）。</summary>
    Task SendMessageAsync(string message, CancellationToken cancellationToken = default);
}
