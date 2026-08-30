namespace Momoka.Core.Configurations;

/// <summary>
/// 配置持久化存储抽象（数据库后端）：以扁平「点分键 = 文本值」行存取配置值树
/// （如 <c>server.port = "8080"</c>）；值文本由 <see cref="DatabaseConfiguration"/> 类型嗅探解释，
/// 存储层不解释语义。实现方可落到任意 KV / 关系表。
/// </summary>
public interface IConfigurationStore
{
    /// <summary>读取全部行（点分键 → 文本值）。</summary>
    IReadOnlyDictionary<string, string?> ReadAll();

    /// <summary>整体写入（替换）全部行。</summary>
    void WriteAll(IReadOnlyDictionary<string, string?> values);
}
