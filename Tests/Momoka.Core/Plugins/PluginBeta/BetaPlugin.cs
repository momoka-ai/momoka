using Momoka.Core.Plugins;

namespace Momoka.Core.Tests.Plugins.Beta;

/// <summary>
/// 依赖 alpha 的插件：StartAsync 经服务注册表解析 alpha 注册的服务（跨程序集解析），
/// 验证依赖拓扑排序与插件间程序集解析。
/// </summary>
public sealed class BetaPlugin : CorePlugin
{
    public override Task StartAsync(CancellationToken cancellationToken)
    {
        Alpha.ITestService service = Plugin.Services.Resolve<Alpha.ITestService>();
        ResolvedGreeting = service.Greeting;
        StartCount++;
        Lifecycle.Record("beta", "start");
        return Task.CompletedTask;
    }

    public override Task StopAsync(CancellationToken cancellationToken)
    {
        StopCount++;
        Lifecycle.Record("beta", "stop");
        return Task.CompletedTask;
    }

    public static string? ResolvedGreeting { get; private set; }

    public static int StartCount { get; set; }

    public static int StopCount { get; set; }
}
