using Momoka.Core.Services;
using Xunit;

namespace Momoka.Core.Tests;

/// <summary>
/// Service&lt;T&gt; 泛型静态注册表：先到先得 / 可选提供商 fallback / 显式覆盖 /
/// 按来源移除与当前提升 / 无注册解析。每测试用独有闭式接口（ITest&lt;T&gt; 特化）隔离静态表。
/// </summary>
public sealed class ServiceTests
{
    private interface ITest<T>
    {
    }

    private sealed class Provider<T> : ITest<T>
    {
    }

    private sealed class A
    {
    }

    private sealed class B
    {
    }

    private sealed class C
    {
    }

    private sealed class D
    {
    }

    private sealed class E
    {
    }

    private sealed class F
    {
    }

    private sealed class G
    {
    }

    private sealed class H
    {
    }

    [Fact]
    public void TryRegister_FirstBecomesCurrent_SecondBecomesFallback()
    {
        object srcA = new();
        object srcB = new();
        var first = new Provider<A>();
        var second = new Provider<A>();

        Assert.True(Service<ITest<A>>.TryRegister(first, srcA));
        Assert.False(Service<ITest<A>>.TryRegister(second, srcB));

        Assert.Same(first, Service<ITest<A>>.Current);
        Assert.Same(first, Service<ITest<A>>.Resolve());
        Assert.Equal(new[] { first, second }, Service<ITest<A>>.All);
        Assert.Equal(new[] { first, second }, Service<ITest<A>>.Registrations.Select(r => r.Provider));
    }

    [Fact]
    public void Register_ExplicitOverwrite_PromotesToCurrent_DemotesPrevious()
    {
        var first = new Provider<B>();
        var replacement = new Provider<B>();
        Service<ITest<B>>.TryRegister(first, new object());

        Service<ITest<B>>.Register(replacement, new object());

        Assert.Same(replacement, Service<ITest<B>>.Current);
        Assert.Equal(new[] { replacement, first }, Service<ITest<B>>.All);
    }

    [Fact]
    public void Register_SameProviderInstance_Deduplicates()
    {
        var provider = new Provider<C>();

        Service<ITest<C>>.TryRegister(provider, new object());
        Service<ITest<C>>.Register(provider, new object());

        Assert.Single(Service<ITest<C>>.Registrations);
    }

    [Fact]
    public void Resolve_NoRegistration_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => Service<ITest<D>>.Resolve());
    }

    [Fact]
    public void TryResolve_NoRegistration_ReturnsNull()
    {
        Assert.Null(Service<ITest<E>>.TryResolve());
        Assert.Null(Service<ITest<E>>.Current);
    }

    [Fact]
    public void Remove_RemovesAllFromSource_AndPromotesFallback()
    {
        object srcA = new();
        object srcB = new();
        var first = new Provider<F>();
        var second = new Provider<F>();
        var third = new Provider<F>();
        Service<ITest<F>>.TryRegister(first, srcA);
        Service<ITest<F>>.TryRegister(second, srcA);
        Service<ITest<F>>.TryRegister(third, srcB);

        Assert.Equal(2, Service<ITest<F>>.Remove(srcA));
        Assert.Same(third, Service<ITest<F>>.Current);
        Assert.Equal(new[] { third }, Service<ITest<F>>.All);

        Assert.Equal(1, Service<ITest<F>>.Remove(srcB));
        Assert.Empty(Service<ITest<F>>.Registrations);
        Assert.Null(Service<ITest<F>>.Current);
    }

    [Fact]
    public void Remove_UnknownSource_ReturnsZero()
    {
        Service<ITest<G>>.TryRegister(new Provider<G>(), new object());

        Assert.Equal(0, Service<ITest<G>>.Remove(new object()));
    }

    [Fact]
    public void NullArguments_Throw()
    {
        Assert.Throws<ArgumentNullException>(() => Service<ITest<H>>.TryRegister(null!));
        Assert.Throws<ArgumentNullException>(() => Service<ITest<H>>.Register(null!));
        Assert.Throws<ArgumentNullException>(() => Service<ITest<H>>.Remove(null!));
    }
}
