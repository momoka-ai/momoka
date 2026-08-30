using Momoka.Core.Commands.Parsing;

namespace Momoka.Core.Commands.Arguments;

/// <summary>固定文本参数：匹配指定字面量（如子命令词），解析值为命中的 token。</summary>
public sealed class LiteralArgument : Argument
{
    public LiteralArgument(string literal)
        : base(literal)
    {
        Literal = literal;
    }

    /// <summary>要匹配的固定文本。</summary>
    public string Literal { get; }

    /// <inheritdoc />
    public override ArgumentQueryResult Parse(string input) =>
        input == Literal
            ? ArgumentQueryResult.Success(input)
            : ArgumentQueryResult.Failure($"Expected literal '{Literal}'.");
}
