namespace Momoka.Core.Commands.Parsing;

/// <summary>
/// 参数解析器（对应 Minestom 中 Argument 的解析职责）：把单个输入 token 解析为类型化值，
/// 产出 <see cref="ArgumentQueryResult"/>。指令参数（<c>Argument</c> 家族）实现本契约；
/// 匹配失败 → <see cref="ArgumentQueryResult.Failure"/>。
/// </summary>
public abstract class ArgumentParser
{
    /// <summary>解析输入 token 为类型化值。</summary>
    public abstract ArgumentQueryResult Parse(string input);
}
