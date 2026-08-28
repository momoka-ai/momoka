using Xunit;
using Momoka.Core.Events;

namespace Momoka.Core.Tests;

/// <summary>事件中心：顺序分发（优先级排序）/并行发布/异常隔离/退订令牌/无订阅者/快照。</summary>
public sealed class EventHubTests
{
    [Fact]
    public async Task Sequential_InvokesHandlersInSubscriptionOrder()
    {
        var hub = new EventHub();
        var calls = new List<string>();

        using var first = hub.Subscribe<string>(s =>
        {
            calls.Add($"first:{s}");
            return Task.CompletedTask;
        });
        using var second = hub.Subscribe<string>(s =>
        {
            calls.Add($"second:{s}");
            return Task.CompletedTask;
        });

        await hub.InvokeAsync("x");

        Assert.Equal(new[] { "first:x", "second:x" }, calls);
    }

    [Fact]
    public async Task Sequential_HandlerException_IsIsolatedAndDoesNotBlockOthers()
    {
        var hub = new EventHub();
        var calls = new List<string>();

        using var throwing = hub.Subscribe<string>(_ =>
        {
            calls.Add("boom");
            throw new InvalidOperationException("handler failure");
        });
        using var following = hub.Subscribe<string>(_ =>
        {
            calls.Add("ok");
            return Task.CompletedTask;
        });

        await hub.InvokeAsync("x"); // 不抛出

        Assert.Equal(new[] { "boom", "ok" }, calls);
    }

    [Fact]
    public async Task Parallel_RunsAllHandlersConcurrently()
    {
        var hub = new EventHub();
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        using var first = hub.Subscribe<int>(async _ =>
        {
            started.TrySetResult();
            await release.Task;
        });
        using var second = hub.Subscribe<int>(async _ =>
        {
            started.TrySetResult();
            await release.Task;
        });

        var publish = hub.InvokeParallelAsync(1);

        // 两个 handler 都在 release 前开始执行 → 并行分发成立
        await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.False(publish.IsCompleted);

        release.SetResult();
        await publish.WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task Parallel_HandlerException_DoesNotPropagate()
    {
        var hub = new EventHub();

        using var throwing = hub.Subscribe<int>(_ => throw new InvalidOperationException("boom"));
        using var ok = hub.Subscribe<int>(_ => Task.CompletedTask);
        await hub.InvokeParallelAsync(1); // 不抛出
    }

    [Fact]
    public async Task UnsubscribeToken_StopsDelivering()
    {
        var hub = new EventHub();
        var calls = new List<string>();

        var token = hub.Subscribe<string>(s =>
        {
            calls.Add(s);
            return Task.CompletedTask;
        });

        await hub.InvokeAsync("a");
        token.Dispose();
        await hub.InvokeAsync("b");

        Assert.Equal(new[] { "a" }, calls);
    }

    [Fact]
    public async Task DisposeToken_IsIdempotent()
    {
        var hub = new EventHub();
        var calls = 0;

        var token = hub.Subscribe<string>(_ =>
        {
            calls++;
            return Task.CompletedTask;
        });

        token.Dispose();
        token.Dispose();
        await hub.InvokeAsync("x");

        Assert.Equal(0, calls);
    }

    [Fact]
    public async Task NoSubscribers_IsNoOp()
    {
        var hub = new EventHub();

        await hub.InvokeAsync(42); // 不抛出
    }

    [Fact]
    public async Task Publish_SnapshotsSubscriptions_UnsubscribeDuringDispatchDoesNotAffectCurrentBatch()
    {
        var hub = new EventHub();
        var calls = new List<string>();
        IDisposable? second = null;

        using var first = hub.Subscribe<int>(_ =>
        {
            calls.Add("first");
            second?.Dispose();
            return Task.CompletedTask;
        });
        second = hub.Subscribe<int>(_ =>
        {
            calls.Add("second");
            return Task.CompletedTask;
        });

        await hub.InvokeAsync(1);
        Assert.Equal(new[] { "first", "second" }, calls);

        await hub.InvokeAsync(2);
        Assert.Equal(new[] { "first", "second", "first" }, calls);
    }

    [Fact]
    public async Task Publish_NullEvent_Throws()
    {
        var hub = new EventHub();

        await Assert.ThrowsAsync<ArgumentNullException>(() => hub.InvokeAsync<string>(null!));
        await Assert.ThrowsAsync<ArgumentNullException>(() => hub.InvokeParallelAsync<string>(null!));
    }

    [Fact]
    public void Subscribe_NullHandler_Throws()
    {
        var hub = new EventHub();

        Assert.Throws<ArgumentNullException>(() => hub.Subscribe<string>(null!));
    }
}
