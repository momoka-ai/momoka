using Momoka.Core.Events;
using Momoka.Core.Plugins;

namespace Momoka.Core.Tests.Plugins.Events;

/// <summary>客户端上报事件（wire-in）：监听者处理，绝不自动广播回客户端。</summary>
[EventRouter(Id = "report_event", Destination = EventDestination.Listeners, FromClients = true)]
public sealed record ReportEvent(string Message);

/// <summary>全向事件（监听者 + 广播）：wire-in 处理后由插件按需生成并发布。</summary>
[EventRouter(Id = "announce_event", Destination = EventDestination.Everyone)]
public sealed record AnnounceEvent(string Message);

/// <summary>
/// 路由/订阅测试插件：OnEnable 用 AddSubscribers(this) 扫描 [EventSubscribe] 订阅，
/// OnDisable 释放整体令牌；收到 ReportEvent 后发布 AnnounceEvent（Everyone → 广播回全部终端）。
/// </summary>
public sealed class EventsPlugin : Plugin
{
    private static readonly object LogGate = new();
    private static readonly List<string> LogList = new();

    private IDisposable? _subscriptions;

    /// <summary>清空跨测试共享的静态日志（测试夹具用）。</summary>
    public static void Reset()
    {
        lock (LogGate)
        {
            LogList.Clear();
        }
    }

    /// <summary>监听日志快照（格式 <c>report:&lt;message&gt;</c>）。</summary>
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
        _subscriptions = Host.Events.AddSubscribers(this);
    }

    public override void OnDisable()
    {
        _subscriptions?.Dispose();
        _subscriptions = null;
    }

    [EventSubscribe(typeof(ReportEvent), Priority = EventPriority.High)]
    public Task OnReport(ReportEvent @event)
    {
        lock (LogGate)
        {
            LogList.Add($"report:{@event.Message}");
        }

        return Host.Events.InvokeAsync(new AnnounceEvent(@event.Message));
    }
}
