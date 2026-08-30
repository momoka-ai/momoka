namespace Momoka.Core.Commands.Arguments;

/// <summary>
/// 内置参数工厂（对应 Minestom 的 <c>ArgumentType</c>）。每个参数带 id（执行器内取值键）；
/// 可选性经 <see cref="Argument{T}.WithDefaultValue(T)"/> 设置（非 null 缺省值即可选）。
/// </summary>
// CA1720：工厂方法名（String/Integer/Double）与 Minestom ArgumentType 保持一致，故意保留类型名。
#pragma warning disable CA1720
public static class ArgumentType
{
    /// <summary>固定文本参数（匹配指定字面量，不产生上下文值）。</summary>
    public static LiteralArgument Literal(string literal) => new(literal);

    /// <summary>字符串参数（引号分词后的 token → string，引号控制值内空格）。</summary>
    public static StringArgument String(string id) => new(id);

    /// <summary>变长字符串参数（消费剩余全部 token，空格拼接 → string）。</summary>
    public static StringArrayArgument StringArray(string id) => new(id);

    /// <summary>布尔参数（true/false）。</summary>
    public static BooleanArgument Boolean(string id) => new(id);

    /// <summary>整数参数（可选 min/max 区间）。</summary>
    public static IntegerArgument Integer(string id) => new(id);

    /// <summary>浮点参数（可选 min/max 区间）。</summary>
    public static DoubleArgument Double(string id) => new(id);

    /// <summary>枚举参数（按名称匹配，忽略大小写）。</summary>
    public static EnumArgument<TEnum> Enum<TEnum>(string id)
        where TEnum : struct, Enum
        => new(id);
}
#pragma warning restore CA1720
