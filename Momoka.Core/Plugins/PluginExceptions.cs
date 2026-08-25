namespace Momoka.Core.Plugins;

/// <summary>
/// 插件结构不合法：DLL 不可加载 / entry 类型不存在、非 <see cref="CorePlugin"/> 或无法实例化 /
/// 重复插件名 / 依赖环 / 签名校验失败等。宿主加载期 fail-fast 抛出。
/// </summary>
public sealed class InvalidPluginException : Exception
{
    public InvalidPluginException()
    {
    }

    public InvalidPluginException(string message)
        : base(message)
    {
    }

    public InvalidPluginException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

/// <summary>
/// 插件信息不合法：缺少可读的 plugin.toml，或 plugin.toml 无法正确解析（TOML 格式非法 /
/// 缺少关键字段 / 类型不符）。宿主加载期 fail-fast 抛出。
/// </summary>
public sealed class InvalidInfoException : Exception
{
    public InvalidInfoException()
    {
    }

    public InvalidInfoException(string message)
        : base(message)
    {
    }

    public InvalidInfoException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

/// <summary>依赖不存在：插件 dependsOn 引用了未知或当前不可用的插件。</summary>
public sealed class UnknownDependencyException : Exception
{
    public UnknownDependencyException()
    {
    }

    public UnknownDependencyException(string message)
        : base(message)
    {
    }

    public UnknownDependencyException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
