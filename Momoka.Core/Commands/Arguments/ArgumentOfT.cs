using Momoka.Core.Commands.Parsing;

namespace Momoka.Core.Commands.Arguments;

/// <summary>
/// 泛型参数基类：<see cref="TryParse(string, out T)"/> 产出强类型值；
/// <see cref="WithDefaultValue(T)"/> 设置缺省值（非 null 即可选）。
/// </summary>
public abstract class Argument<T> : Argument
{
    protected Argument(string id)
        : base(id)
    {
    }

    /// <summary>尝试解析输入 token 为 <typeparamref name="T"/>（不合法返回 false）。</summary>
    public abstract bool TryParse(string input, out T value);

    /// <summary>设置缺省值（非 null 即标记可选）。</summary>
    public Argument<T> WithDefaultValue(T? defaultValue)
    {
        base.WithDefaultValue(defaultValue);
        return this;
    }

    /// <inheritdoc />
    public override ArgumentQueryResult Parse(string input) =>
        TryParse(input, out T value)
            ? ArgumentQueryResult.Success(value)
            : ArgumentQueryResult.Failure($"Value '{input}' is not a valid '{typeof(T).Name}'.");
}
