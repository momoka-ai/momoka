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

/// <summary>正常插件：OnEnable 注册服务，OnDisable 清理由插件自行完成，生命周期记录到共享文件。</summary>
public sealed class AlphaPlugin : Plugin
{
    public override void OnEnable()
    {
        EnableCount++;
        Host.Services.Register<ITestService>(new TestService(), plugin: this);
        Lifecycle.Record("alpha", "enable");
    }

    public override void OnDisable()
    {
        DisableCount++;
        Lifecycle.Record("alpha", "disable");
    }

    public static int EnableCount { get; set; }

    public static int DisableCount { get; set; }
}
