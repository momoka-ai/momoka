using Momoka.Core.Plugins;

namespace Momoka.Core.Tests.Plugins.Beta;

/// <summary>
/// 依赖 alpha 的插件：OnEnable 经服务注册表解析 alpha 注册的服务（跨程序集解析），
/// 验证依赖拓扑排序与插件间程序集解析。
/// </summary>
public sealed class BetaPlugin : Plugin
{
    public override void OnEnable()
    {
        Alpha.ITestService service = Host.Services.Resolve<Alpha.ITestService>();
        ResolvedGreeting = service.Greeting;
        EnableCount++;
        Lifecycle.Record("beta", "enable");
    }

    public override void OnDisable()
    {
        DisableCount++;
        Lifecycle.Record("beta", "disable");
    }

    public static string? ResolvedGreeting { get; private set; }

    public static int EnableCount { get; set; }

    public static int DisableCount { get; set; }
}
