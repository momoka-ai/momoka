using Momoka.Core.Events;
using Momoka.Core.Plugins;

namespace Momoka.Core.Tests.Plugins.Events;

/// <summary>全向通知事件（[Publish] 契约：可传输，发布即广播全部终端 + 分发进程内监听者）。</summary>
[Publish]
public sealed record AnnounceEvent(string Message);

/// <summary>
/// 事件/广播集成测试插件：OnEnable 订阅自身（AddSubscribers）并广播 "enabled"
/// （插件 → 事件总线 → wire-out 全部终端），OnDisable 整体退订。
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

    /// <summary>监听日志快照（格式 <c>announce:&lt;message&gt;</c>）。</summary>
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
        Host.Events.InvokeAsync(new AnnounceEvent("enabled"));
    }

    public override void OnDisable()
    {
        Host.Events.RemoveSubscribers(this);
    }

    [Subscribe(typeof(AnnounceEvent))]
    public Task OnAnnounce(AnnounceEvent @event)
    {
        lock (LogGate)
        {
            LogList.Add($"announce:{@event.Message}");
        }

        return Task.CompletedTask;
    }
}
