using Xunit;
using Momoka.Core.Events;

namespace Momoka.Core.Tests;

/// <summary>事件总线：顺序/异常隔离/并行/后台/退订令牌/无订阅者/快照。</summary>
public sealed class EventBusTests
{
    [Fact]
    public async Task Sequential_InvokesHandlersInSubscriptionOrder()
    {
        var bus = new EventBus();
        var calls = new List<string>();

        using var first = bus.Subscribe<string>(s =>
        {
            calls.Add($"first:{s}");
            return Task.CompletedTask;
        });
        using var second = bus.Subscribe<string>(s =>
        {
            calls.Add($"second:{s}");
            return Task.CompletedTask;
        });

        await bus.PublishAsync("x");

        Assert.Equal(new[] { "first:x", "second:x" }, calls);
    }

    [Fact]
    public async Task Sequential_HandlerException_IsIsolatedAndDoesNotBlockOthers()
    {
        var bus = new EventBus();
        var calls = new List<string>();

        using var throwing = bus.Subscribe<string>(_ =>
        {
            calls.Add("boom");
            throw new InvalidOperationException("handler failure");
        });
        using var following = bus.Subscribe<string>(_ =>
        {
            calls.Add("ok");
            return Task.CompletedTask;
        });

        await bus.PublishAsync("x"); // 不抛出

        Assert.Equal(new[] { "boom", "ok" }, calls);
    }

    [Fact]
    public async Task Parallel_RunsAllHandlersAndCompletes()
    {
        var bus = new EventBus();
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        using var first = bus.Subscribe<int>(async _ =>
        {
            started.TrySetResult();
            await release.Task;
        });
        using var second = bus.Subscribe<int>(async _ =>
        {
            started.TrySetResult();
            await release.Task;
        });

        var publish = bus.PublishAsync(1, DispatchMode.Parallel);

        // 两个 handler 都在 release 前开始执行 → 并行分发成立
        await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.False(publish.IsCompleted);

        release.SetResult();
        await publish.WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task Parallel_HandlerException_DoesNotPropagate()
    {
        var bus = new EventBus();

        using var throwing = bus.Subscribe<int>(_ => throw new InvalidOperationException("boom"));
        using var ok = bus.Subscribe<int>(_ => Task.CompletedTask);
        await bus.PublishAsync(1, DispatchMode.Parallel);
    }

    [Fact]
    public async Task Background_FireAndForget_StillInvokesHandlers()
    {
        var bus = new EventBus();
        var invoked = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        using var handler = bus.Subscribe<string>(_ =>
        {
            invoked.TrySetResult();
            return Task.CompletedTask;
        });

        await bus.PublishAsync("x", DispatchMode.Background);
        await invoked.Task.WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task UnsubscribeToken_StopsDelivering()
    {
        var bus = new EventBus();
        var calls = new List<string>();

        var token = bus.Subscribe<string>(s =>
        {
            calls.Add(s);
            return Task.CompletedTask;
        });

        await bus.PublishAsync("a");
        token.Dispose();
        await bus.PublishAsync("b");

        Assert.Equal(new[] { "a" }, calls);
    }

    [Fact]
    public async Task DisposeToken_IsIdempotent()
    {
        var bus = new EventBus();
        var calls = 0;

        var token = bus.Subscribe<string>(_ =>
        {
            calls++;
            return Task.CompletedTask;
        });

        token.Dispose();
        token.Dispose();
        await bus.PublishAsync("x");

        Assert.Equal(0, calls);
    }

    [Fact]
    public async Task NoSubscribers_IsNoOp()
    {
        var bus = new EventBus();

        await bus.PublishAsync(42); // 不抛出
    }

    [Fact]
    public async Task Publish_SnapshotsSubscriptions_UnsubscribeDuringDispatchDoesNotAffectCurrentBatch()
    {
        var bus = new EventBus();
        var calls = new List<string>();
        IDisposable? second = null;

        using var first = bus.Subscribe<int>(_ =>
        {
            calls.Add("first");
            second?.Dispose();
            return Task.CompletedTask;
        });
        second = bus.Subscribe<int>(_ =>
        {
            calls.Add("second");
            return Task.CompletedTask;
        });

        await bus.PublishAsync(1);
        Assert.Equal(new[] { "first", "second" }, calls);

        await bus.PublishAsync(2);
        Assert.Equal(new[] { "first", "second", "first" }, calls);
    }

    [Fact]
    public async Task Publish_NullEvent_Throws()
    {
        var bus = new EventBus();

        await Assert.ThrowsAsync<ArgumentNullException>(() => bus.PublishAsync<string>(null!));
    }

    [Fact]
    public void Subscribe_NullHandler_Throws()
    {
        var bus = new EventBus();

        Assert.Throws<ArgumentNullException>(() => bus.Subscribe<string>(null!));
    }
}
