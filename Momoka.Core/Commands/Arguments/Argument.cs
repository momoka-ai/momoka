using Momoka.Core.Commands.Parsing;

namespace Momoka.Core.Commands.Arguments;

/// <summary>
/// 指令参数基类（对应 Minestom 的 <c>Argument&lt;T&gt;</c>）：id（执行器内取值键）+
/// 缺省值 <see cref="DefaultValue"/>（null = 必需；非 null = 可选，是否可选由 CommandSyntax 依此控制）。
/// 值是否含空格由输入引号决定（分词器归一 token）；匹配为固定元数——每个槽位恰好一个 token。
/// 解析契约继承 <see cref="ArgumentParser"/>。
/// </summary>
public abstract class Argument : ArgumentParser
{
    protected Argument(string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        Id = id;
    }

    /// <summary>参数 id：语法内唯一，执行器内按此取值。</summary>
    public string Id { get; }

    /// <summary>
    /// 缺省值：null = 必需；非 null = 可选（缺失时由语法注入）。
    /// 对应 Minestom 的 <c>isOptional ≔ defaultValue ≠ null</c>。
    /// </summary>
    public object? DefaultValue { get; private set; }

    /// <summary>设置缺省值（非 null 即标记可选）。</summary>
    public Argument WithDefaultValue(object? defaultValue)
    {
        DefaultValue = defaultValue;
        return this;
    }
}
