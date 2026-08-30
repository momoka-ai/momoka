namespace Momoka.Core.Configurations;

/// <summary>
/// 配置版本迁移步：从 <see cref="From"/> 升级到 <see cref="To"/>，<see cref="Apply"/> 就地修改
/// 配置值树。旧配置**向上升级**、未知字段保留（**向后兼容**）——迁移只增改已知键，不动其余数据。
/// </summary>
public sealed class Migration
{
    /// <summary>创建迁移步（from 必须小于 to；来源版本重复 fail-fast，见 <see cref="Configuration"/>）。</summary>
    public Migration(Version from, Version to, Action<Configuration> apply)
    {
        From = from ?? throw new ArgumentNullException(nameof(from));
        To = to ?? throw new ArgumentNullException(nameof(to));
        Apply = apply ?? throw new ArgumentNullException(nameof(apply));
        if (from >= to)
        {
            throw new ArgumentException($"Migration target '{to}' must be newer than source '{from}'.", nameof(to));
        }
    }

    /// <summary>迁移来源版本（旧配置所在版本）。</summary>
    public Version From { get; }

    /// <summary>迁移目标版本。</summary>
    public Version To { get; }

    /// <summary>迁移动作：就地修改配置值树（经 Get/Set 访问）。</summary>
    public Action<Configuration> Apply { get; }
}
