using Momoka.Core.Events;
using Momoka.Core.Plugins;

namespace Momoka.Core.Tests.Plugins.RouterBad;

/// <summary>与 <see cref="DupEventB"/> 撞 eventId —— 插件加载注册表填充时 fail-fast（重复 Id）。</summary>
[EventRouter(Id = "dup_event", Destination = EventDestination.Everyone)]
public sealed record DupEventA(string Value);

/// <summary>与 <see cref="DupEventA"/> 撞 eventId。</summary>
[EventRouter(Id = "dup_event", Destination = EventDestination.Everyone)]
public sealed record DupEventB(string Value);

/// <summary>非法路由插件（仅测试夹具：重复 eventId 触发 <see cref="InvalidOperationException"/>）。</summary>
public sealed class RouterBadPlugin : Plugin
{
}
