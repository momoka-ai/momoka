using Microsoft.Extensions.Logging;
using Momoka.Core.Behaviors;

namespace Momoka.Core.Plugins;

/// <summary>
/// 插件宿主能力束（宿主级共享，全插件共用同一实例）：统一持有服务注册表 / 事件中心 / 网关设施 /
/// 日志工厂与运行时插件根目录（Plugins，硬编码于基目录之下）。
/// 插件专属能力（日志器 / 插件目录 / 配置）由 <see cref="Plugin"/> 基于自身名称派生。
/// </summary>
public sealed class PluginService
{
    /// <summary>创建宿主级能力束。插件根目录位于 <paramref name="baseDirectory"/>
    /// （缺省 <see cref="AppContext.BaseDirectory"/>）下的 Plugins；<paramref name="gateway"/>
    /// 缺省时自建无 SignalR 宿主的默认网关（单元测试/纯进程内场景）。</summary>
    public PluginService(
        ServiceRegistry services,
        EventHub events,
        ILoggerFactory loggerFactory,
        string? baseDirectory = null,
        Gateway? gateway = null)
    {
        Services = services ?? throw new ArgumentNullException(nameof(services));
        Events = events ?? throw new ArgumentNullException(nameof(events));
        LoggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
        Gateway = gateway ?? new Gateway(events);

        string baseDir = baseDirectory ?? AppContext.BaseDirectory;
        PluginsDirectory = new DirectoryInfo(Path.Combine(baseDir, "Plugins"));
    }

    /// <summary>插件间服务发现表（共享）。</summary>
    public ServiceRegistry Services { get; }

    /// <summary>强类型事件中心（共享）。</summary>
    public EventHub Events { get; }

    /// <summary>Ui 网关设施（共享；操作经 <c>RegisterOperation</c> 注册，行为由插件加载期扫描注册）。</summary>
    public Gateway Gateway { get; }

    /// <summary>日志工厂（共享；插件日志器经 <see cref="Plugin"/> 派生）。</summary>
    public ILoggerFactory LoggerFactory { get; }

    /// <summary>插件根目录（&lt;base&gt;/Plugins，插件各占一个子目录）。</summary>
    public DirectoryInfo PluginsDirectory { get; }
}
