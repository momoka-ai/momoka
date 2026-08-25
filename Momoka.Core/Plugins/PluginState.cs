namespace Momoka.Core.Plugins;

/// <summary>插件生命周期状态机：Discovered → Loaded → Started → Stopped / Failed。</summary>
public enum PluginState
{
    /// <summary>已从程序集发现（manifest 已解析）。</summary>
    Discovered = 0,

    /// <summary>已实例化并 Load（<c>OnLoad</c> 已运行）。</summary>
    Loaded = 1,

    /// <summary>已 <c>StartAsync</c>。</summary>
    Started = 2,

    /// <summary>已 <c>StopAsync</c>。</summary>
    Stopped = 3,

    /// <summary>生命周期失败（启动失败回滚或停止失败）。</summary>
    Failed = 4,
}
