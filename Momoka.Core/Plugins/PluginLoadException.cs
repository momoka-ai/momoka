namespace Momoka.Core.Plugins;

/// <summary>
/// 插件宿主失败统一出口（fail-fast）：manifest 解析错误 / 重复插件名 / entry 非法 /
/// 依赖未知或禁用 / 依赖环 / 加载或生命周期失败回滚后抛出，inner 为原始异常。
/// </summary>
public sealed class PluginLoadException : Exception
{
    public PluginLoadException()
    {
    }

    public PluginLoadException(string message)
        : base(message)
    {
    }

    public PluginLoadException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
