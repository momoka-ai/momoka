namespace Momoka.Core.Plugins;

/// <summary>
/// 插件契约（运行期扩展单元）：身份（Name/Version）与生命周期（Start/Stop）。
/// 模块（静态子工程）实现本契约即成为可被宿主加载的插件。
/// </summary>
public interface IPlugin
{
    /// <summary>插件名，全局唯一；与 manifest 的 <c>name</c> 交叉校验（由宿主回填）。</summary>
    string Name { get; }

    /// <summary>插件版本；与 manifest 的 <c>version</c> 交叉校验（由宿主回填）。</summary>
    string Version { get; }

    /// <summary>启动插件。宿主保证依赖插件已先行启动、宿主能力已注入。</summary>
    Task StartAsync(CancellationToken cancellationToken);

    /// <summary>停止插件。宿主按加载逆序调用（best-effort）。</summary>
    Task StopAsync(CancellationToken cancellationToken);
}
