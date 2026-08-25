using Momoka.Core.Plugins;

namespace Momoka.Core.Tests.Plugins.Explode;

/// <summary>
/// 可注入失败的插件：测试经静态开关（测试经反射设置）触发 Load / Start 失败，
/// 验证宿主逆序回滚已启动插件。
/// </summary>
public sealed class ExplodePlugin : CorePlugin
{
    protected override void OnLoad()
    {
        if (ThrowOnLoad)
        {
            throw new InvalidOperationException("simulated load failure");
        }
    }

    public override Task StartAsync(CancellationToken cancellationToken)
    {
        if (ThrowOnStart)
        {
            throw new InvalidOperationException("simulated start failure");
        }

        return Task.CompletedTask;
    }

    public override Task StopAsync(CancellationToken cancellationToken)
    {
        StopCount++;
        return Task.CompletedTask;
    }

    public static bool ThrowOnLoad { get; set; }

    public static bool ThrowOnStart { get; set; }

    public static int StopCount { get; set; }
}
