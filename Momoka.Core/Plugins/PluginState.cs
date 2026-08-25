namespace Momoka.Core.Plugins;

/// <summary>插件生命周期状态：Load 后即 Loaded；EnableAsync 成功 → Enabled；DisableAsync 成功 → Disabled；生命周期回调失败 → Failed。</summary>
public enum PluginState
{
    /// <summary>已加载（Load 完成，实例化并记录）。</summary>
    Loaded = 0,

    /// <summary>已启用（OnEnable 已成功执行）。</summary>
    Enabled = 1,

    /// <summary>已停用（OnDisable 已成功执行）。</summary>
    Disabled = 2,

    /// <summary>生命周期失败（OnEnable / OnDisable 抛出异常）。</summary>
    Failed = 3,
}
