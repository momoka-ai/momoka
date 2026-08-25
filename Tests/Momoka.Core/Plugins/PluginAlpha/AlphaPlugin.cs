using Momoka.Core.Plugins;

namespace Momoka.Core.Tests.Plugins.Alpha;

/// <summary>测试插件注册的业务服务（仅测试夹具）。</summary>
public interface ITestService
{
    string Greeting { get; }
}

/// <summary>测试业务服务实现。</summary>
public sealed class TestService : ITestService
{
    public string Greeting => "hello from alpha";
}

/// <summary>正常插件：OnLoad 注册服务，Start/Stop 记录生命周期到共享文件。</summary>
public sealed class AlphaPlugin : CorePlugin
{
    protected override void OnLoad()
    {
        Plugin.Services.Register<ITestService>(new TestService());
    }

    public override Task StartAsync(CancellationToken cancellationToken)
    {
        StartCount++;
        Lifecycle.Record("alpha", "start");
        return Task.CompletedTask;
    }

    public override Task StopAsync(CancellationToken cancellationToken)
    {
        StopCount++;
        Lifecycle.Record("alpha", "stop");
        return Task.CompletedTask;
    }

    public static int StartCount { get; set; }

    public static int StopCount { get; set; }
}
