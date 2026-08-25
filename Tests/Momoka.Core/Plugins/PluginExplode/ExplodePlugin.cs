using Momoka.Core.Plugins;

namespace Momoka.Core.Tests.Plugins.Explode;

/// <summary>
/// 可注入失败的插件：测试经静态开关（测试经反射设置）触发 OnEnable / OnDisable 失败，
/// 验证宿主状态机（Failed）与批量启停回滚。
/// </summary>
public sealed class ExplodePlugin : Plugin
{
    public override void OnEnable()
    {
        if (ThrowOnEnable)
        {
            throw new InvalidOperationException("simulated enable failure");
        }
    }

    public override void OnDisable()
    {
        if (ThrowOnDisable)
        {
            throw new InvalidOperationException("simulated disable failure");
        }

        DisableCount++;
    }

    public static bool ThrowOnEnable { get; set; }

    public static bool ThrowOnDisable { get; set; }

    public static int DisableCount { get; set; }
}
