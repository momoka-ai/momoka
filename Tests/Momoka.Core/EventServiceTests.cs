using Momoka.Core.Events;
using Momoka.Core.Plugins;
using Xunit;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using EventHandler = Momoka.Core.Events.EventHandler;

namespace Momoka.Core.Tests;

/// <summary>
/// 事件服务（唯一总线，收编原 EventHub）：Add/AddRange 注册（重复实例 fail-fast）／
/// Add&lt;T&gt;(Action&lt;T&gt;, Plugin?) 就地封装临时处理器并返回可移除句柄／Remove/RemoveRange 退订
/// （引用同一性、幂等）、Remove(predicate) 跨桶清理／Send 按运行时类型精确路由、按优先级降序
/// （同级注册序）派发／SendAsync 线程池派发／handler 异常原样传播并停止。装配由插件侧完成，此处不再扫描。
/// </summary>
public sealed class EventServiceTests
{
    [Fact]
    public void Send_InvokesMatchingHandlers_InRegistrationOrder()
    {
        var events = new EventService();
        var calls = new List<string>();

        events.Add(CreateMessageHandler(e => calls.Add($"first:{e.Text}")));
        events.Add(CreateMessageHandler(e => calls.Add($"second:{e.Text}")));

        events.Send(new MessageEvent("x"));

        Assert.Equal(new[] { "first:x", "second:x" }, calls);
    }

    [Fact]
    public void Send_RoutesByRuntimeEventType()
    {
        var events = new EventService();
        var calls = new List<string>();

        events.Add(new EventHandler(new Owner(), null, typeof(MessageEvent), e => calls.Add($"msg:{((MessageEvent)e).Text}"), EventPriority.Normal));
        events.Add(new EventHandler(new Owner(), null, typeof(OtherEvent), e => calls.Add("other"), EventPriority.Normal));

        events.Send(new MessageEvent("hi"));
        events.Send(new OtherEvent());

        Assert.Equal(new[] { "msg:hi", "other" }, calls);
    }

    [Fact]
    public void Send_InvokesInPriorityOrder_StableForSamePriority()
    {
        var events = new EventService();
        var calls = new List<string>();

        events.Add(CreateMessageHandler(_ => calls.Add("normal-a"), EventPriority.Normal));
        events.Add(CreateMessageHandler(_ => calls.Add("high"), EventPriority.High));
        events.Add(CreateMessageHandler(_ => calls.Add("normal-b"), EventPriority.Normal));
        events.Add(CreateMessageHandler(_ => calls.Add("low"), EventPriority.Low));

        events.Send(new MessageEvent("x"));

        Assert.Equal(new[] { "high", "normal-a", "normal-b", "low" }, calls);
    }

    [Fact]
    public async Task SendAsync_DeliversAll_AndCompletesOnAwait()
    {
        var events = new EventService();
        var calls = new List<string>();

        events.Add(CreateMessageHandler(e => calls.Add(e.Text)));

        await events.SendAsync(new MessageEvent("hi"));

        Assert.Equal(new[] { "hi" }, calls);
    }

    [Fact]
    public void Add_DuplicateInstance_FailsFast_ReAddAfterRemove()
    {
        var events = new EventService();
        EventHandler handler = CreateMessageHandler(_ => { });
        events.Add(handler);

        Assert.Throws<InvalidOperationException>(() => events.Add(handler));

        events.Remove(handler);
        events.Add(handler);
        events.Send(new MessageEvent("x")); // 不抛
    }

    [Fact]
    public void AddRange_RegistersAll_RemoveRange_UnregistersAll()
    {
        var events = new EventService();
        var calls = new List<string>();
        var handlers = new[]
        {
            CreateMessageHandler(e => calls.Add($"a:{e.Text}")),
            CreateMessageHandler(e => calls.Add($"b:{e.Text}")),
        };

        events.AddRange(handlers);
        events.Send(new MessageEvent("x"));
        Assert.Equal(new[] { "a:x", "b:x" }, calls);

        events.RemoveRange(handlers);
        events.Send(new MessageEvent("y"));
        Assert.Equal(2, calls.Count);
    }

    [Fact]
    public void Remove_StopsDelivery_IsIdempotent_ReAddWorks()
    {
        var events = new EventService();
        var calls = new List<string>();
        EventHandler handler = CreateMessageHandler(e => calls.Add(e.Text));
        events.Add(handler);

        events.Remove(handler);
        events.Remove(handler); // 幂等
        events.Send(new MessageEvent("a"));
        Assert.Empty(calls);

        events.Add(handler);
        events.Send(new MessageEvent("b"));
        Assert.Equal(new[] { "b" }, calls);
    }

    [Fact]
    public void Add_TypedTemporaryHandler_Invokes_AndRemovesByHandle()
    {
        var events = new EventService();
        var plugin = new Plugin(CreatePluginInfo());
        var calls = new List<string>();

        EventHandler handle = events.Add<MessageEvent>(e => calls.Add(e.Text), plugin);
        events.Send(new MessageEvent("hi"));

        Assert.Equal(new[] { "hi" }, calls);
        Assert.Null(handle.Owner);
        Assert.Same(plugin, handle.Plugin);
        Assert.Equal(typeof(MessageEvent), handle.EventType);

        events.Remove(handle);
        events.Send(new MessageEvent("bye"));
        Assert.Equal(new[] { "hi" }, calls);
    }

    [Fact]
    public void Remove_Predicate_CleansAcrossBuckets()
    {
        var events = new EventService();
        var calls = new List<string>();
        EventHandler message = CreateMessageHandler(e => calls.Add($"m:{e.Text}"));
        events.Add(message);
        events.Add(new EventHandler(new Owner(), null, typeof(OtherEvent), _ => calls.Add("o"), EventPriority.Normal));

        events.Remove(h => ReferenceEquals(h, message));
        events.Send(new MessageEvent("x"));
        events.Send(new OtherEvent());

        Assert.Equal(new[] { "o" }, calls);
    }

    [Fact]
    public void Send_TypedShell_RoutesByRuntimeType()
    {
        var events = new EventService();
        var calls = new List<string>();
        events.Add(CreateMessageHandler(e => calls.Add($"msg:{e.Text}")));

        events.Send(new MessageEvent("hi"));
        events.Send(new MessageEvent("again"));

        Assert.Equal(new[] { "msg:hi", "msg:again" }, calls);
    }

    [Fact]
    public void Send_HandlerException_PropagatesAndStops()
    {
        var events = new EventService();
        var calls = new List<string>();

        events.Add(CreateMessageHandler(_ => throw new InvalidOperationException("boom")));
        events.Add(CreateMessageHandler(e => calls.Add(e.Text)));

        Assert.Throws<InvalidOperationException>(() => events.Send(new MessageEvent("x")));
        Assert.Empty(calls);
    }

    [Fact]
    public void Send_NoSubscribers_IsNoOp()
    {
        var events = new EventService();

        events.Send(new MessageEvent("x")); // 不抛出
    }

    private static EventHandler CreateMessageHandler(Action<MessageEvent> action, EventPriority priority = EventPriority.Normal)
        => new(new Owner(), null, typeof(MessageEvent), e => action((MessageEvent)e), priority);

    private static PluginInfo CreatePluginInfo() => new()
    {
        Name = "tmp",
        Version = "1.0.0",
        Main = "tmp.Entry, Fake",
    };

    private sealed record class MessageEvent(string Text) : Event;

    private sealed record class OtherEvent : Event;

    private sealed class Owner : IEventHandler
    {
    }
}
