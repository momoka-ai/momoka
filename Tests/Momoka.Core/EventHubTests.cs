using Momoka.Core.Events;
using Xunit;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using EventHandler = Momoka.Core.Events.EventHandler;

namespace Momoka.Core.Tests;

/// <summary>
/// 事件中心（纯注册/分发表）：Add/AddRange 注册（重复实例 fail-fast）／Remove/RemoveRange 退订
/// （引用同一性、幂等）／Send 按运行时类型精确路由、按优先级降序（同级注册序）派发／
/// SendAsync 线程池派发／handler 异常原样传播并停止。装配由插件侧完成，此处不再扫描。
/// </summary>
public sealed class EventHubTests
{
    [Fact]
    public void Send_InvokesMatchingHandlers_InRegistrationOrder()
    {
        var hub = new EventHub();
        var calls = new List<string>();

        hub.Add(CreateMessageHandler(e => calls.Add($"first:{e.Text}")));
        hub.Add(CreateMessageHandler(e => calls.Add($"second:{e.Text}")));

        hub.Send(new MessageEvent("x"));

        Assert.Equal(new[] { "first:x", "second:x" }, calls);
    }

    [Fact]
    public void Send_RoutesByRuntimeEventType()
    {
        var hub = new EventHub();
        var calls = new List<string>();

        hub.Add(new EventHandler(new Owner(), typeof(MessageEvent), e => calls.Add($"msg:{((MessageEvent)e).Text}"), EventPriority.Normal));
        hub.Add(new EventHandler(new Owner(), typeof(OtherEvent), e => calls.Add("other"), EventPriority.Normal));

        hub.Send(new MessageEvent("hi"));
        hub.Send(new OtherEvent());

        Assert.Equal(new[] { "msg:hi", "other" }, calls);
    }

    [Fact]
    public void Send_InvokesInPriorityOrder_StableForSamePriority()
    {
        var hub = new EventHub();
        var calls = new List<string>();

        hub.Add(CreateMessageHandler(_ => calls.Add("normal-a"), EventPriority.Normal));
        hub.Add(CreateMessageHandler(_ => calls.Add("high"), EventPriority.High));
        hub.Add(CreateMessageHandler(_ => calls.Add("normal-b"), EventPriority.Normal));
        hub.Add(CreateMessageHandler(_ => calls.Add("low"), EventPriority.Low));

        hub.Send(new MessageEvent("x"));

        Assert.Equal(new[] { "high", "normal-a", "normal-b", "low" }, calls);
    }

    [Fact]
    public async Task SendAsync_DeliversAll_AndCompletesOnAwait()
    {
        var hub = new EventHub();
        var calls = new List<string>();

        hub.Add(CreateMessageHandler(e => calls.Add(e.Text)));

        await hub.SendAsync(new MessageEvent("hi"));

        Assert.Equal(new[] { "hi" }, calls);
    }

    [Fact]
    public void Add_DuplicateInstance_FailsFast_ReAddAfterRemove()
    {
        var hub = new EventHub();
        EventHandler handler = CreateMessageHandler(_ => { });
        hub.Add(handler);

        Assert.Throws<InvalidOperationException>(() => hub.Add(handler));

        hub.Remove(handler);
        hub.Add(handler);
        hub.Send(new MessageEvent("x")); // 不抛
    }

    [Fact]
    public void AddRange_RegistersAll_RemoveRange_UnregistersAll()
    {
        var hub = new EventHub();
        var calls = new List<string>();
        var handlers = new[]
        {
            CreateMessageHandler(e => calls.Add($"a:{e.Text}")),
            CreateMessageHandler(e => calls.Add($"b:{e.Text}")),
        };

        hub.AddRange(handlers);
        hub.Send(new MessageEvent("x"));
        Assert.Equal(new[] { "a:x", "b:x" }, calls);

        hub.RemoveRange(handlers);
        hub.Send(new MessageEvent("y"));
        Assert.Equal(2, calls.Count);
    }

    [Fact]
    public void Remove_StopsDelivery_IsIdempotent_ReAddWorks()
    {
        var hub = new EventHub();
        var calls = new List<string>();
        EventHandler handler = CreateMessageHandler(e => calls.Add(e.Text));
        hub.Add(handler);

        hub.Remove(handler);
        hub.Remove(handler); // 幂等
        hub.Send(new MessageEvent("a"));
        Assert.Empty(calls);

        hub.Add(handler);
        hub.Send(new MessageEvent("b"));
        Assert.Equal(new[] { "b" }, calls);
    }

    [Fact]
    public void Send_HandlerException_PropagatesAndStops()
    {
        var hub = new EventHub();
        var calls = new List<string>();

        hub.Add(CreateMessageHandler(_ => throw new InvalidOperationException("boom")));
        hub.Add(CreateMessageHandler(e => calls.Add(e.Text)));

        Assert.Throws<InvalidOperationException>(() => hub.Send(new MessageEvent("x")));
        Assert.Empty(calls);
    }

    [Fact]
    public void Send_NoSubscribers_IsNoOp()
    {
        var hub = new EventHub();

        hub.Send(new MessageEvent("x")); // 不抛出
    }

    private static EventHandler CreateMessageHandler(Action<MessageEvent> action, EventPriority priority = EventPriority.Normal)
        => new(new Owner(), typeof(MessageEvent), e => action((MessageEvent)e), priority);

    private sealed record class MessageEvent(string Text) : Event;

    private sealed record class OtherEvent : Event;

    private sealed class Owner : IEventHandler
    {
    }
}
