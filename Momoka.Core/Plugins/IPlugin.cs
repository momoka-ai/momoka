namespace Momoka.Core.Plugins;

/// <summary>
/// 插件只读契约：名称与版本（与 manifest 交叉校验，由宿主注入回填）。
/// 生命周期（OnEnable / OnDisable）与宿主能力见 <see cref="Plugin"/> 基类。
/// </summary>
public interface IPlugin
{
    /// <summary>插件名（全局唯一，与 manifest.name 一致）。</summary>
    string Name { get; }

    /// <summary>插件版本（与 manifest.version 一致）。</summary>
    string Version { get; }
}
