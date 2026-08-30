using Momoka.Core.Behaviors;
using Momoka.Core.Plugins;

namespace Momoka.Core.Tests.Plugins.RouterBad;

/// <summary>非法行为夹具（缺 Execute 方法）→ 插件加载期 <see cref="Gateway.RegisterBehavior"/> fail-fast。</summary>
public sealed class BadBehavior : Behavior
{
    /// <summary>事实（携带 [Publish] 契约）。</summary>
    [Publish]
    public sealed record Event(string Message);

    /// <summary>意图。</summary>
    public sealed record Intent(string Message);

    // 无 Execute —— 触发注册校验失败
}

/// <summary>非法路由插件（仅测试夹具：缺 Execute 的行为触发 <see cref="InvalidOperationException"/>）。</summary>
public sealed class RouterBadPlugin : Plugin
{
}
