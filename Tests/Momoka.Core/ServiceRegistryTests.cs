using Xunit;
using Momoka.Core.Plugins;

namespace Momoka.Core.Tests;

/// <summary>服务注册表（富 API）：注册/优先级解析/多注册枚举（按类型·按插件）/缺失/类型校验/并发冒烟。</summary>
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

    private sealed class FakePlugin : Plugin
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
    public void Resolve_HighestPriority_Wins()
    {
        var registry = new ServiceRegistry();
        var normal = new FooService();
        var highest = new FooService();

        registry.Register<IFooService>(normal, ServicePriority.Normal);
        registry.Register<IFooService>(highest, ServicePriority.Highest);

        Assert.Same(highest, registry.Resolve<IFooService>());
    }

    [Fact]
    public void Resolve_SamePriority_FirstRegisteredWins()
    {
        var registry = new ServiceRegistry();
        var first = new FooService();
        var second = new FooService();

        registry.Register<IFooService>(first);
        registry.Register<IFooService>(second);

        Assert.Same(first, registry.Resolve<IFooService>());
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
    public void GetService_TryGetService_ReturnHighestPriority()
    {
        var registry = new ServiceRegistry();
        var high = new FooService();
        registry.Register<IFooService>(new FooService(), ServicePriority.Low);
        registry.Register<IFooService>(high, ServicePriority.High);

        Assert.Same(high, registry.TryResolve<IFooService>());
        Assert.True(registry.TryGetService<IFooService>(out var value));
        Assert.Same(high, value);

        Assert.Null(registry.TryResolve<IBarService>());
        Assert.False(registry.TryGetService<IBarService>(out _));
    }

    [Fact]
    public void Register_InstanceNotAssignable_ThrowsArgumentException()
    {
        var registry = new ServiceRegistry();

        Assert.Throws<ArgumentException>(() => registry.Register(typeof(IFooService), "not a foo"));
    }

    [Fact]
    public void GetRegistrations_ReturnsAll_OrderedByPriority()
    {
        var registry = new ServiceRegistry();
        var normal = new FooService();
        var high = new FooService();

        registry.Register<IFooService>(normal, ServicePriority.Normal);
        registry.Register<IFooService>(high, ServicePriority.High);

        var registrations = registry.GetRegistrations<IFooService>().ToList();

        Assert.Equal(2, registrations.Count);
        Assert.Same(high, registrations[0].Source);
        Assert.Same(normal, registrations[1].Source);
        Assert.Equal(ServicePriority.High, registrations[0].Priority);
    }

    [Fact]
    public void GetRegistrations_ByRuntimeType_ReturnsMatchingEntries()
    {
        var registry = new ServiceRegistry();
        registry.Register(typeof(FooService), new FooService());
        registry.Register(typeof(IFooService), new FooService());

        var registrations = registry.GetRegistrations<IFooService>(typeof(FooService)).ToList();

        Assert.Single(registrations);
        Assert.Equal(typeof(FooService), registrations[0].Service);
    }

    [Fact]
    public void GetRegistrations_ByPlugin_ReturnsOnlyThatPluginsServices()
    {
        var registry = new ServiceRegistry();
        var alpha = new FakePlugin();
        var beta = new FakePlugin();
        var alphaService = new FooService();
        var betaService = new FooService();

        registry.Register<IFooService>(alphaService, plugin: alpha);
        registry.Register<IFooService>(betaService, plugin: beta);

        var alphaRegistrations = registry.GetRegistrations<IFooService>(alpha).ToList();

        Assert.Single(alphaRegistrations);
        Assert.Same(alphaService, alphaRegistrations[0].Source);
        Assert.Same(alpha, alphaRegistrations[0].Plugin);
    }

    [Fact]
    public void GetRegistrations_UnknownType_IsEmpty()
    {
        var registry = new ServiceRegistry();

        Assert.Empty(registry.GetRegistrations<IFooService>());
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
                registry.Register<IFooService>(new FooService());
            }))
            .ToList();

        start.SetResult();
        await Task.WhenAll(tasks);

        Assert.True(registry.IsRegistered(typeof(IFooService)));
        Assert.NotNull(registry.Resolve<IFooService>());
    }
}
