using Momoka.Core.Behaviors;
using Momoka.Core.Plugins;

namespace Momoka.Core.Tests.Plugins.Events;

/// <summary>全向通知事件（[Publish] 契约：可传输，发布即广播全部终端 + 分发进程内监听者）。</summary>
[Publish]
public sealed record AnnounceEvent(string Message);

/// <summary>
/// 行为测试夹具：客户端 Post 意图 → 主机 Execute 生成事实（[Publish]，下行广播）→ 监听者可见。
/// Execute 记录来源日志并返回事实（四件套契约由插件加载期扫描注册到 Gateway）。
/// </summary>
public sealed class GreetBehavior : Behavior<GreetBehavior>
{
    /// <summary>事实（下行广播载荷，只由主机生成）。</summary>
    [Publish]
    public sealed record Event(string Message);

    /// <summary>意图（上行请求载荷，客户端唯一构造的对象）。</summary>
    public sealed record Intent(string Message);

    /// <summary>逻辑执行：意图 → 事实。</summary>
    public Event Execute(Intent intent, IntentSource? source = null)
    {
        lock (EventsPlugin.LogGate)
        {
            EventsPlugin.LogList.Add($"greet:{intent.Message}");
        }

        return new Event(intent.Message);
    }
}

/// <summary>
/// 路由/订阅测试插件：OnEnable 用 AddSubscribers(this) 扫描 [Subscribe] 订阅（载体实现 Subscribers），
/// OnDisable 用 RemoveSubscribers 整体退订；监听 GreetBehavior 事实。
/// </summary>
public sealed class EventsPlugin : Plugin, Subscribers
{
    internal static readonly object LogGate = new();
    internal static readonly List<string> LogList = new();

    /// <summary>清空跨测试共享的静态日志（测试夹具用）。</summary>
    public static void Reset()
    {
        lock (LogGate)
        {
            LogList.Clear();
        }
    }

    /// <summary>监听日志快照（格式 <c>greet:&lt;message&gt;</c> / <c>fact:&lt;message&gt;</c>）。</summary>
    public static IReadOnlyList<string> Log
    {
        get
        {
            lock (LogGate)
            {
                return LogList.ToList();
            }
        }
    }

    public override void OnEnable()
    {
        Host.Events.AddSubscribers(this);
    }

    public override void OnDisable()
    {
        Host.Events.RemoveSubscribers(this);
    }

    [Subscribe(typeof(GreetBehavior.Event))]
    public Task OnGreetFact(GreetBehavior.Event @event)
    {
        lock (LogGate)
        {
            LogList.Add($"fact:{@event.Message}");
        }

        return Task.CompletedTask;
    }
}
