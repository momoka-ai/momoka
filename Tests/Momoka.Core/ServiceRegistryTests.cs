using Xunit;
using Momoka.Core.Registry;

namespace Momoka.Core.Tests;

/// <summary>服务注册表：注册/解析/缺失/类型校验/重复注册/并发冒烟。</summary>
public sealed class ServiceRegistryTests
{
    private interface IFooService
    {
    }

    private interface IBarService
    {
    }

    private sealed class FooService : IFooService
    {
    }

    [Fact]
    public void RegisterAndResolve_RoundTrips()
    {
        var registry = new ServiceRegistry();
        var service = new FooService();

        registry.Register<IFooService>(service);

        Assert.Same(service, registry.Resolve<IFooService>());
    }

    [Fact]
    public void RegisterByType_AcceptsAssignableInstance()
    {
        var registry = new ServiceRegistry();
        var service = new FooService();

        registry.Register(typeof(IFooService), service);

        Assert.True(registry.IsRegistered(typeof(IFooService)));
    }

    [Fact]
    public void TryResolve_Missing_ReturnsNull()
    {
        var registry = new ServiceRegistry();

        Assert.Null(registry.TryResolve<IFooService>());
    }

    [Fact]
    public void Resolve_Missing_ThrowsInvalidOperationException()
    {
        var registry = new ServiceRegistry();

        var ex = Assert.Throws<InvalidOperationException>(() => registry.Resolve<IFooService>());
        Assert.Contains(typeof(IFooService).ToString(), ex.Message);
    }

    [Fact]
    public void Register_InstanceNotAssignable_ThrowsArgumentException()
    {
        var registry = new ServiceRegistry();

        Assert.Throws<ArgumentException>(() => registry.Register(typeof(IFooService), "not a foo"));
    }

    [Fact]
    public void Register_Duplicate_ThrowsInvalidOperationException()
    {
        var registry = new ServiceRegistry();

        registry.Register<IFooService>(new FooService());
        Assert.Throws<InvalidOperationException>(() => registry.Register<IFooService>(new FooService()));
    }

    [Fact]
    public void IsRegistered_ReflectsState()
    {
        var registry = new ServiceRegistry();

        Assert.False(registry.IsRegistered(typeof(IFooService)));
        registry.Register<IFooService>(new FooService());
        Assert.True(registry.IsRegistered(typeof(IFooService)));
        Assert.False(registry.IsRegistered(typeof(IBarService)));
    }

    [Fact]
    public void Register_NullArguments_Throw()
    {
        var registry = new ServiceRegistry();

        Assert.Throws<ArgumentNullException>(() => registry.Register<IFooService>(null!));
        Assert.Throws<ArgumentNullException>(() => registry.Register(typeof(IFooService), null!));
        Assert.Throws<ArgumentNullException>(() => registry.Register(null!, new FooService()));
    }

    [Fact]
    public async Task ConcurrentRegisterResolve_Smoke()
    {
        var registry = new ServiceRegistry();
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var tasks = Enumerable.Range(0, 8)
            .Select(_ => Task.Run(async () =>
            {
                await start.Task;
                try
                {
                    registry.Register<IFooService>(new FooService());
                }
                catch (InvalidOperationException)
                {
                    // 并发下仅一个能注册成功，其余 fail-fast
                }
            }))
            .ToList();

        start.SetResult();
        await Task.WhenAll(tasks);

        Assert.True(registry.IsRegistered(typeof(IFooService)));
    }
}
