namespace Momoka.Core.Configurations;

/// <summary>配置子系统异常（fail-fast）：值缺失 / 类型不符 / 迁移断链 / 持久化失败。</summary>
public sealed class ConfigurationException : Exception
{
    public ConfigurationException(string message)
        : base(message)
    {
    }

    public ConfigurationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
